# PublishingTracker MCP & Agent — Testing & Verification Guide

This guide details how to verify, test, and troubleshoot both the **PublishingTracker MCP Server** (`/mcp` inside `PublishingTracker.Api`), **PublishingTracker.Agent** (`POST /api/agent/query`), and the **PublishingTracker.Rag** module across **Phases 0–4 (Steps 4.1–4.5 implemented)**.

---

## 1. Test Architecture & Coverage (`106/106` Implemented Tests Passing)

| Test Assembly / Filter | Phase Coverage | Count | Test Approach |
|---|---|---:|---|
| `PublishingTracker.Api.Tests` | Baseline API & Phase 0 (`TokenService`, `FormatCompletenessRules`) | `41` | Unit tests + `TestWebAppFactory` integration suite |
| `Phase1McpAndCatalogueTests` | Phase 1 Hosted `/mcp` & REST parity | `13` *(executions across 8 methods)* | Real `ModelContextProtocol.Client.McpClient` over Streamable HTTP against `TestWebAppFactory` + `TwoUserSeedCatalog` |
| `AgentLoopTests` (`PublishingTracker.Agent.Tests`) | Phase 2 Agent Loop, Auth Forwarding, Provider Routing & Telemetry | `20` | Deterministic `ScriptedChatClient`, `StubToolProvider`, and `RecordingHttpMessageHandler` (`0` external API spend) |
| `Phase3EvidenceAndValidationTests` (`PublishingTracker.Agent.Tests`) | Phase 3 Linked `EvidenceLedger`, Stage 2 Repair & Strict Output Validation | `22` | Deterministic `ScriptedChatClient` + stub MCP/DTO responses (`0` external API spend) |
| `Phase4RagRetrievalAndIsolationTests` (`PublishingTracker.Agent.Tests`) | Phase 4 Steps 4.1–4.5 RAG VectorMath, Markdown Chunker, Ingestion, Embeddings, Retrieval, and Composite Provider Isolation | `10` | Deterministic `DeterministicTestEmbeddingGenerator`, EF InMemory/Npgsql double, and `CompositeToolProvider` stubs |
| **Total Implemented CI Suite** | **Phases 0–4 (Steps 4.1–4.5)** | **`106`** | **100% deterministic offline execution** |

The MCP integration tests run against `TestWebAppFactory` with a real `ModelContextProtocol.Client.McpClient` over Streamable HTTP:

```
┌────────────────────────────────────────────────────────┐
│ Phase1McpAndCatalogueTests                             │
│   ├── Real McpClient (HttpClientTransport)             │
│   └── Seed: TwoUserSeedCatalog (User A vs User B)      │
└──────────────────────────┬─────────────────────────────┘
                           │ Streamable HTTP (POST /mcp)
                           ▼
┌────────────────────────────────────────────────────────┐
│ TestWebAppFactory (In-Memory PostgreSQL Test Host)     │
│   ├── /mcp endpoint                                    │
│   └── Two-User Isolated Database Catalog               │
└────────────────────────────────────────────────────────┘
```

### The Two-User Test Matrix (`TwoUserSeedCatalog.cs`)
To prove non-model-controlled tenant isolation, the test fixture seeds a deterministic multi-user catalog:

| User | Book Title | Formats | State |
|---|---|---|---|
| **User A** (`author-a@example.test`) | *Complete Catalogue Title* | Paperback, Ebook | Complete (all fields present) |
| **User A** | *Free Promo Ebook* | Ebook | Complete (Price = $0.00 is valid) |
| **User A** | *Audio Missing Narrator* | Audiobook | **Incomplete:** Missing `Narrator`, `Duration` |
| **User A** | *Clockwork Missing Book Metadata* | Hardcover | **Incomplete:** Has `SeriesName` but missing `SeriesNumber` |
| **User A** | *Draft With No Editions* | None | **Incomplete:** Zero editions attached |
| **User B** (`beta-author@example.test`) | *Beta Private Incomplete Hardcover* | Hardcover | **Incomplete:** Owned exclusively by User B |

---

## 2. Automated Test Execution

### Running All Implemented Tests (Phases 0–4 Steps 4.1–4.5 — `106/106` Passing)
Run the full solution test suite using standard `dotnet test` flags for clean Windows execution:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT="1"; $env:DOTNET_NOLOGO="1"
dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln `
  --no-restore -m:1 /nodeReuse:false /p:UseSharedCompilation=false
```

**Expected Output:**
- `PublishingTracker.Api.Tests.dll`: `Passed! - Failed: 0, Passed: 54, Skipped: 0, Total: 54`
- `PublishingTracker.Agent.Tests.dll`: `Passed! - Failed: 0, Passed: 52, Skipped: 0, Total: 52` (including `10/10` Phase 4 RAG tests)

### Running Filtered Suites by Phase

```powershell
# Phase 1: Hosted /mcp endpoint & REST parity (13 test executions)
dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln --no-restore --filter "FullyQualifiedName~Phase1"

# Phase 2: Agent loop budgets, auth forwarding, routing & tracing (20 tests)
dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln --no-restore --filter "FullyQualifiedName~AgentLoopTests"

# Phase 3: Linked EvidenceLedger, Stage 2 repair & strict output validation (22 tests)
dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln --no-restore --filter "FullyQualifiedName~Phase3EvidenceAndValidationTests"

# Phase 4: RAG VectorMath, MarkdownChunker, Ingestion, Embeddings, Retrieval, and Composite Tool Provider (10 tests)
dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln --no-restore --filter "FullyQualifiedName~Phase4RagRetrievalAndIsolationTests"
```

*(Note: `Phase1McpAndCatalogueTests` contains 8 test methods, with `McpClient_InvalidArguments_ReturnsActionableErrors` running as an xUnit theory with 6 test cases, totaling 13 executions.)*

---

## 3. Automated Test Scenarios Explained

| Test Name | What It Verifies |
|---|---|
| `McpEndpoint_WithoutBearerToken_Returns401Unauthorized` | Calling `POST /mcp` without an `Authorization: Bearer <token>` header immediately yields HTTP 401. |
| `McpClient_ListTools_DiscoversOnlyExpectedReadOnlyToolsWithoutTenantParams` | Standalone `McpClient` discovers exactly `find_titles_missing_metadata` and `get_title_details`. Confirms `ReadOnly = true` and verifies zero `userId` or tenant parameters in the JSON schemas. |
| `McpClient_FindTitlesMissingMetadata_ReturnsIncompleteUserABooksAndRespectsFilters` | Calls `find_titles_missing_metadata` as User A. Asserts it returns only the 3 incomplete titles for User A, ignores complete titles, and excludes User B's book. Verifies format filter (`Audiobook`) and field filter (`SeriesNumber`). |
| `McpClient_FindTitlesMissingMetadata_PaginatesCorrectlyWithHasMore` | Verifies bounded pagination (`page=1, pageSize=2` returns 2 items and `hasMore: true`; `page=2, pageSize=2` returns 1 item and `hasMore: false`). |
| `McpClient_GetTitleDetails_ReturnsFullBookEditionsAndPlatformSummary` | Calls `get_title_details` as User A on their audiobook title. Verifies book metadata, edition metadata, format completeness flags, and Kindle Direct platform publication summary. |
| `McpClient_GetTitleDetails_TwoUserIsolation_MasksOtherUsersBook` | **Security Invariant:** User A calls `get_title_details` with User B's `bookId`. Proves the server returns an identical `NOT_FOUND_OR_UNAUTHORIZED` error as a non-existent ID (999999), preventing cross-tenant existence enumeration. Proves User B can read their own book. |
| `McpClient_InvalidArguments_ReturnsActionableErrors` | Theory testing invalid parameters: `page: 0`, `pageSize: 51`, `format: "Laserdisc"`, `format: "1"`, `missingField: "Bogus"`, and `bookId: 0`. Asserts all return actionable `INVALID_ARGUMENT:` errors. |
| `RestEndpoints_MissingMetadataAndDetails_MatchMcpContractsAndIsolation` | Confirms identical behavior and status codes (`200`, `400`, `401`, `404`) across the REST routes (`/api/books/missing-metadata` and `/api/books/{id}/details`). |

---

## 4. Manual Verification Against Live API (`curl` / PowerShell)

To test a live running instance of `PublishingTracker.Api`:

### Step 1: Start the API
```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project Publishing-Tracker/apps/api/src/PublishingTracker.Api
```
*(Default launch profile listens at `https://localhost:7285` / `http://localhost:5226`)*

### Step 2: Acquire a JWT Token
If testing against an existing database seeded with test accounts:
```bash
curl -k -X POST https://localhost:7285/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"author-a@example.test","password":"Password123!"}'
```
*(Tip: If testing against a fresh, empty development database, first register a user via `POST /api/auth/register` with `{"email":"author-a@example.test","password":"Password123!","firstName":"Author","lastName":"Alpha"}`)*

Set the returned token to a variable:
```powershell
$token = "<token_from_login>"
```

### Step 3: MCP Handshake (`initialize`)
Send a standard JSON-RPC 2.0 `initialize` request to `POST /mcp`:

```bash
curl -k -X POST https://localhost:7285/mcp \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
      "protocolVersion": "2025-06-18",
      "capabilities": {},
      "clientInfo": { "name": "manual-tester", "version": "1.0" }
    }
  }'
```

**Expected Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "protocolVersion": "2025-06-18",
    "capabilities": { "tools": { "listChanged": false } },
    "serverInfo": { "name": "PublishingTracker.Api", "version": "1.0.0" }
  }
}
```

### Step 4: Discover Tools (`tools/list`)
```bash
curl -k -X POST https://localhost:7285/mcp \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/list",
    "params": {}
  }'
```

**Verification Checklist:**
- [ ] Exactly two tools appear: `find_titles_missing_metadata` and `get_title_details`.
- [ ] `readOnly: true` is present on both tool descriptions.
- [ ] No `userId`, `ownerId`, or `callerUserId` appears in `inputSchema.properties`.

### Step 5: Execute `find_titles_missing_metadata`
```bash
curl -k -X POST https://localhost:7285/mcp \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{
    "jsonrpc": "2.0",
    "id": 3,
    "method": "tools/call",
    "params": {
      "name": "find_titles_missing_metadata",
      "arguments": {
        "format": "Audiobook"
      }
    }
  }'
```

**Expected Result:** Returns structured JSON containing incomplete audiobooks with `missingFields: ["Narrator", "Duration"]`.

### Step 6: Verify Two-User Security Isolation
1. Obtain an ID for a book owned by User B (e.g. ID `99`).
2. Make a `tools/call` for `get_title_details` with `"bookId": 99` using User A's token:

```bash
curl -k -X POST https://localhost:7285/mcp \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{
    "jsonrpc": "2.0",
    "id": 4,
    "method": "tools/call",
    "params": {
      "name": "get_title_details",
      "arguments": {
        "bookId": 99
      }
    }
  }'
```

**Expected Result:**
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": {
    "isError": true,
    "content": [
      {
        "type": "text",
        "text": "NOT_FOUND_OR_UNAUTHORIZED: Title was not found or is not accessible."
      }
    ]
  }
}
```
*(Notice: The response masks the book's existence and leaks zero title or author details.)*

---

## 5. Troubleshooting & Diagnostics

| Symptom | Root Cause | Fix |
|---|---|---|
| `HTTP 401 Unauthorized` | Missing or expired JWT, or token missing `Bearer ` prefix. | Re-authenticate at `/api/auth/login`. Ensure the header format is `Authorization: Bearer <token>`. |
| `HTTP 400 Bad Request` | Required Streamable HTTP headers missing. | Include `-H "Accept: application/json, text/event-stream"` on the request. |
| `INVALID_ARGUMENT: Unsupported format '...'` | Passing numeric format (`"1"`) or comma lists (`"Ebook,Paperback"`). | Pass exact declared format name: `Ebook`, `Paperback`, `Hardcover`, `Audiobook`, or `LargeText`. |
| Tool returns text instead of JSON | Client omitting structured content handling. | Ensure client supports `UseStructuredContent = true` (MCP Protocol 2025-06-18+). |
| Handshake hangs or fails | Trying to connect over stdio rather than HTTP. | Point client at `https://localhost:7285/mcp` using `StreamableHttp` transport mode. |
| Agent rejects valid JWT (`401 Unauthorized`) | Token missing required `iat` or user ID claim. | Ensure JWT was minted by updated `TokenService` (`JwtRegisteredClaimNames.Iat` integer64 + `NameId`). |
| Agent startup fails immediately | Unsupported `AIProvider:Type`, non-HTTPS `/mcp` URL, or invalid `AgentLoop` bounds. | Verify `AIProvider:Type` (`openrouter`/`openai`/`ollama`), trusted HTTPS/loopback `McpEndpoint`, and `OverallTimeout > PerCallTimeout`. |

---

## 6. Phase 2 & Phase 3 Automated Agent Test Suites (`PublishingTracker.Agent.Tests`)

All 42 Agent tests run offline with zero API keys or outbound network traffic via `ScriptedChatClient`, `StubToolProvider`, and `RecordingHttpMessageHandler`.

### 6.1 Phase 2 Suite ([AgentLoopTests.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Agent.Tests/AgentLoopTests.cs) — 20 Tests)

| Category | Test Coverage & Hardened Assertions |
|---|---|
| **Happy Path & Empty Text (3)** | `AgentLoop_TwoToolSequence_CompletesWithGroundedAnswer`, `AgentLoop_UnfinishedPagination_EmitsMandatoryLimitations`, `AgentLoop_ModelEmitsEmptyText_HaltsWithEmptyResponseFailure` |
| **Dual Budgets & Canonicalization (6)** | `AgentLoop_MaxModelTurnsExceeded_HaltsWithStructuredFailure`, `AgentLoop_MaxTotalToolCallsExceeded_AcrossMultipleTurns_HaltsWithBudgetError`, `AgentLoop_MaxTotalToolCallsExceeded_WithinSingleTurn_HaltsImmediately`, `AgentLoop_RepeatCallLimitExceeded_WithPermutedArgumentKeys_HaltsWithRepeatCallError`, `AgentLoop_PayloadSizeCap_RejectsExcessivePayload`, `AgentLoop_PaginationLimitExceeded_RejectsPageBeyondCap` |
| **Faults & Cancellation/Timeouts (7)** | `AgentLoop_UnknownToolRequested_HaltsGracefullyWithUnknownToolError`, `AgentLoop_DuplicateMcpToolNames_HaltsWithInvalidToolsetError`, `AgentLoop_ToolThrowsException_HaltsGracefullyWithMaskedError`, `AgentLoop_ToolSerializationFails_HaltsWithSerializationError`, `AgentLoop_ModelCallTimeout_CancelsAndFailsGracefully` (differentiates model timeout vs. caller cancellation), `AgentLoop_ToolCallTimeout_NonCooperativeTool_AbortsExecution` (verifies `toolToken.IsCancellationRequested == true`), `AgentLoop_OverallDeadlineExceeded_HaltsExecution` |
| **Security, Tracing & Providers (4)** | `OpenTelemetry_EmitsSpansWithoutSensitiveData` (verifies `agent.prompt_version == "v1"` and zero leaked secrets/prompts), `PerRequestBearerHandler_AttachesToken_ReplacesExistingHeader_AndRejectsInsecureEndpoints` (verifies single whitespace-free Bearer header, rejecting multi-header smuggling & whitespace tokens), `AIProviderSetup_RoutingAndHeaderTests` (verifies case-insensitive `Validate()`, `customHttpClient` wiring, OpenRouter privacy headers, `AgentLoopOptions`), `AIProviderOptions_ToString_RedactsApiKeys` |

### 6.2 Phase 3 Suite ([Phase3EvidenceAndValidationTests.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Agent.Tests/Phase3EvidenceAndValidationTests.cs) — 22 Tests)

| Area | Test Scenarios Covered |
|---|---|
| **Linked Evidence & Zero-Citation Isolation (8)** | Linked `(bookId, editionId, fields)` verification & host `toolsUsed`; `could_not_answer` without citations; single scoped empty search allowed (including humanized `"series number"` match); empty search followed by non-empty search rejected; uncited answer omitting empty-search filter rejected; mismatched `(bookId, editionId)` repaired; unreturned field repaired; combined book + trimmed `" Narrator "` fields accepted & `"PlatformName"` rejected when `PlatformSummaries` is empty. |
| **Strict JSON, Size & Truncation Disclosure (6)** | Uncorroborated document citation rejected; malformed Stage 2 JSON repaired preserving strict `User -> Assistant -> User` message order; duplicate JSON keys & unmapped properties (`toolsUsed`) rejected; exact UTF-8 byte boundary (`32,768` accepted vs. `32,769` multi-byte rejected); null collections/malformed citation fields rejected; unrelated `Limitations` rejected when `hasMore=true` until pagination truncation disclosed. |
| **MCP Envelope & Pagination Invariants (4)** | Out-of-order page 2 first & mid-query `pageSize` change halted (`PAGINATION_SEQUENCE_ERROR`); returned `page` or `totalCount`/`hasMore` math mismatch halted (`EVIDENCE_INDEXING_ERROR`); malformed tool payload & `get_title_details` requested vs. returned `bookId` mismatch halted; real `CallToolResult` (`structuredContent`) unwrapped cleanly. |
| **Stage 2 Timeouts, Turns & Failure Envelope (4)** | Stage 2 `PerCallTimeout` halts with `TIMEOUT:`; validation retry exhaustion fails with `VALIDATION_RETRIES_EXHAUSTED:` preserving Stage 1 `toolsUsed`; turn budget exhausted at Stage 1 turn 5 (`TURN_LIMIT_EXCEEDED:`) before calling doomed tools and at Stage 2 turn 6; failure envelope rejects internal exception strings across all 18 `AgentErrorCode` values. |

---

## 7. Manual Verification of `PublishingTracker.Agent` (`POST /api/agent/query`)

1. Start `PublishingTracker.Api` in terminal 1 (`https://localhost:7285`, per Section 4 Step 1).
2. Configure secrets and start `PublishingTracker.Agent` in terminal 2 with an explicit listen URL (since `PublishingTracker.Agent` has no default `launchSettings.json`):
   ```powershell
   $env:ASPNETCORE_ENVIRONMENT="Development"
   $env:Jwt__Key="<same-signing-key-as-PublishingTracker.Api>"
   $env:Jwt__Issuer="PublishingTracker.Api"
   $env:Jwt__Audience="PublishingTracker.Api"
   $env:OPENROUTER_API_KEY="<your-openrouter-key>"
   dotnet run --project Publishing-Tracker/apps/api/src/PublishingTracker.Agent --urls https://localhost:7290
   ```
3. Reuse the JWT `$token` obtained from `https://localhost:7285/api/auth/login` (must include an `iat` claim) and query `POST /api/agent/query`:
   ```bash
   curl -k -X POST https://localhost:7290/api/agent/query \
     -H "Authorization: Bearer $token" \
     -H "Content-Type: application/json" \
     -d '{"question": "Find my audiobooks missing metadata and inspect the details."}'
   ```
4. **Checklist:**
   - [ ] `schemaVersion` is `"1.0"` and `status` is `"answered"` (or `"could_not_answer"` with an `AgentErrorCode:` message).
   - [ ] `toolsUsed` lists host-observed `ToolExecutionRecord` objects (`toolName`, `turn`, `durationMs`, `succeeded`).
   - [ ] `evidence.catalogueSources` contains valid `(bookId, editionId?, fields)` citations present in the caller's `/mcp` catalogue.
   - [ ] Unauthenticated requests and JWTs missing `iat` return HTTP `401 Unauthorized`.

---

## 8. Phase 4 RAG Test Matrix (`16` Total Scenarios, `10/16` Implemented)

Specified in [Phase-4-RAG-Module-with-pgvector.md](Phase-4-RAG-Module-with-pgvector.md) (`Phase4RagRetrievalAndIsolationTests.cs`, offline via `DeterministicTestEmbeddingGenerator` and `TestWebAppFactory`/EF Core doubles):

### Implemented & Passing Tests (`10/10`):
1. `VectorMath_ValidatesDimensions_ComputesCosineDistance_AndFormatsSqlLiterals` *(Step 4.1)* — Verifies 1536 dimension check, unit normalization, cosine distance bounds [0, 2], and PostgreSQL `<=>` vector formatting.
2. `MarkdownChunker_SplitsHeadingsDeterministically_AssignsStableChunkIds_AndComputesContentHash` *(Step 4.2)* — Verifies heading chunking, SHA-256 hash stability, and deterministic `{docId}#c{index}` IDs.
3. `MarkdownChunker_OversizedSection_SplitsWithBoundedWindowAndOverlap` *(Step 4.2)* — Verifies bounded 1,000 char / 1,500 UTF-8 byte chunking with 150 char sliding overlap.
4. `MarkdownChunker_FencedCodeBlocksWithHashComments_AndEmptySections_HandledSafely` *(Step 4.2)* — Verifies `# comments` inside triple-backtick fences do not split headers, and empty sections are ignored.
5. `RagIngestion_IdempotentUpsert_UpdatesChangedChunks_AndRemovesStaleChunksOnVersionUpdate` *(Step 4.3)* — Verifies unchanged content hashes are not re-embedded, and stale chunks from older versions are deleted.
6. `RagIngestion_EmbeddingFailureMidDocument_LeavesExistingChunksUntouched` *(Step 4.3)* — Verifies transactional rollback/safety if embedding generation fails mid-document.
7. `AIProviderSetup_CreateEmbeddingGenerator_ConfiguresEndpointAndOpenRouterPrivacyHeaders` *(Step 4.4)* — Verifies OpenRouter privacy headers (`HTTP-Referer`, `X-Title`), `data_collection: "deny"`, and OpenTelemetry without sensitive data.
8. `RagSearchTool_SchemaInspection_ExposesOnlyQueryAndTopK_NeverUserIdOrTenantParameters` *(Step 4.5)* — Verifies tool schema exposes only `(string query, int topK = 5)` and strictly hides `userId` from model schemas.
9. `RagSearchTool_TwoUserIsolation_ReturnsSharedGuidelinesAndCallerNotes_NeverOtherUsersNotes` *(Step 4.5)* — Verifies caller with `userId = 1` receives shared guidelines (`OwnerUserId = null`) and their own private notes, but never User 2's notes.
10. `CompositeToolProvider_UnauthenticatedCaller_AndDownstreamFailure_DisposesMcpSessionCleanly` *(Step 4.5)* — Verifies invalid/unauthenticated caller (`userId <= 0`) is blocked prior to downstream MCP queries, and inner MCP session is disposed cleanly on error.

### Planned for Step 4.6 (`6` Scenarios):
11. `RagSearchTool_InvalidQueryOrTopKArguments_FailsWithStructuredError` *(Step 4.6)*
12. `EvidenceLedger_IndexesSearchPublishingGuidelinesOutput_AndFailsClosedOnMalformedPayload` *(Step 4.6)*
13. `Stage2_CombinedCatalogueAndDocumentCitations_VerifiedAgainstEvidenceLedger_Succeeds` *(Step 4.6)*
14. `Stage2_StaleOrUnreturnedDocumentChunkCitation_RejectedAndRepairedOnRetry` *(Step 4.6)*
15. `PromptInjection_InRetrievedChunk_RequestingUnknownToolOrFabricatedCitation_IsBlockedByHostGuards` *(Step 4.6)*
16. `RagSearchTool_EmptyHitsSearch_BlocksUncitedAnsweredDraft_EvenWhenCatalogueSearchWasEmpty` *(Step 4.6)*
