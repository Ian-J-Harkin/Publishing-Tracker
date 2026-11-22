# PublishingTracker MCP & Agent — Testing & Verification Guide

This guide details how to verify, test, and troubleshoot the **PublishingTracker MCP Server** (`/mcp` inside `PublishingTracker.Api`), **PublishingTracker.Agent** (`POST /api/agent/query`), **PublishingTracker.Rag**, the **Phase 5** evaluation suite (`PublishingTracker.Evals`), and **Phase 6** deployment/security checks across **Phases 0–6 (Complete)**. See also the end-to-end manual test playbook in [Human-User-Testing-Walkthrough.md](Human-User-Testing-Walkthrough.md).

---

## 1. Test Architecture & Coverage (`186/186` Implemented Tests Passing)

| Test Assembly / Filter | Phase Coverage | Count | Test Approach |
|---|---|---:|---|
| `PublishingTracker.Api.Tests` | Baseline API & Phase 0 (`TokenService`, `FormatCompletenessRules`, path-traversal & `Result` safety guards) | `49` | Unit tests + `TestWebAppFactory` integration suite |
| `Phase1McpAndCatalogueTests` | Phase 1 Hosted `/mcp`, REST parity & `UserSecretsId` | `15` *(executions across 10 methods)* | Real `ModelContextProtocol.Client.McpClient` over Streamable HTTP against `TestWebAppFactory` + `TwoUserSeedCatalog` |
| `AgentLoopTests` (`PublishingTracker.Agent.Tests`) | Phase 2 Agent Loop, Auth Forwarding, Provider Routing & Telemetry | `20` | Deterministic `ScriptedChatClient`, `StubToolProvider`, and `RecordingHttpMessageHandler` (`0` external API spend) |
| `Phase3EvidenceAndValidationTests` (`PublishingTracker.Agent.Tests`) | Phase 3 Linked `EvidenceLedger`, Stage 2 Repair & Strict Output Validation | `22` | Deterministic `ScriptedChatClient` + stub MCP/DTO responses (`0` external API spend) |
| `Phase4RagRetrievalAndIsolationTests` (`PublishingTracker.Agent.Tests`) | Phase 4 RAG VectorMath, Markdown Chunker, Ingestion, Embeddings, Retrieval, Composite Provider Isolation & Prompt-Injection Defense | `16` | Deterministic `DeterministicTestEmbeddingGenerator`, EF InMemory/Npgsql double, and `CompositeToolProvider` stubs |
| `Phase6DeploymentAndSecurityTests` (`PublishingTracker.Agent.Tests`) | Phase 6 Container Security, `AllowInsecureInternalHttp` Production Guard, `IsEndpointTrusted`, 32-byte `Jwt:Key`, `gemini` / `/run/secrets` & `*_FILE` resolution, `UserSecretsId`, blank `appsettings.json` secrets gate, `DevelopmentCatalogSeeder`, and `HealthProbe` | `25` *(executions across 14 methods)* | Unit and loopback TCP integration tests covering environment guards, secret mounts, user secrets, zero committed secrets, endpoint allowlists, bearer forwarding, demo seeding, and console healthcheck probing |
| `PublishingTracker.Evals` | Phase 5 Layer 1 (30 deterministic golden/evaluator tests), Layer 2 (4 real `pgvector` & multi-tenant tests), Layer 3 (5 opt-in live smoke & baseline tests) | `39` | Deterministic evaluators + PostgreSQL `pgvector` fixture (port 5435 / `TEST_POSTGRES_CONNECTION`) + opt-in live smoke tests |
| **Total Implemented CI Suite** | **Phases 0–6** | **`186`** | **100% deterministic offline & PostgreSQL integration (`Category!=LiveEval` = 181 tests; `Category=LiveEval` = 5 opt-in tests)** |

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
| **User B** (`author-b@example.test`) | *Beta Private Incomplete Hardcover* | Hardcover | **Incomplete:** Owned exclusively by User B |

---

## 2. Automated Test Execution

### Running All Implemented Tests (Phases 0–6 — `186/186` Total, `181/181` CI Non-Live Filter)
Run the CI solution test suite using standard `dotnet test` flags for clean Windows execution:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT="1"; $env:DOTNET_NOLOGO="1"
dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln `
  --no-restore -m:1 /nodeReuse:false /p:UseSharedCompilation=false --filter "Category!=LiveEval"
```

**Expected Output:**
- `PublishingTracker.Api.Tests.dll`: `Passed! - Failed: 0, Passed: 64, Skipped: 0, Total: 64`
- `PublishingTracker.Agent.Tests.dll`: `Passed! - Failed: 0, Passed: 83, Skipped: 0, Total: 83` (including `16/16` Phase 4 RAG tests and `25/25` Phase 6 deployment/security tests)
- `PublishingTracker.Evals.dll`: `Passed! - Failed: 0, Passed: 34, Skipped: 0, Total: 34` (30 Layer 1 deterministic + 4 Layer 2 real pgvector integration; 5 `Category=LiveEval` tests run separately when opted in, for `39` total in `PublishingTracker.Evals`)

### Running Filtered Suites by Phase

```powershell
# Phase 1: Hosted /mcp endpoint, REST parity & UserSecretsId (15 test executions)
dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln --no-restore --filter "FullyQualifiedName~Phase1"

# Phase 2: Agent loop budgets, auth forwarding, routing & tracing (20 tests)
dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln --no-restore --filter "FullyQualifiedName~AgentLoopTests"

# Phase 3: Linked EvidenceLedger, Stage 2 repair & strict output validation (22 tests)
dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln --no-restore --filter "FullyQualifiedName~Phase3EvidenceAndValidationTests"

# Phase 4: RAG VectorMath, chunking, ingestion, retrieval, and composite tool provider (16 tests)
dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln --no-restore --filter "FullyQualifiedName~Phase4RagRetrievalAndIsolationTests"

# Phase 5: Continuous evaluation harness (Deterministic Layer 1 + Real pgvector Layer 2 = 34 tests)
dotnet test Publishing-Tracker/apps/api/tests/PublishingTracker.Evals --no-restore --filter "Category!=LiveEval"

# Phase 5: Opt-in Live Smoke Evals (5 tests, requires live AI provider credentials for 3 end-to-end LLM tests)
$env:RUN_LIVE_EVALS="true"
$env:GEMINI_API_KEY="<your-gemini-key>"
dotnet test Publishing-Tracker/apps/api/tests/PublishingTracker.Evals --filter "Category=LiveEval"

# Phase 6: Container security, AllowInsecureInternalHttp guard, 32-byte JWT, UserSecretsId, /run/secrets, appsettings check, HealthProbe (25 tests)
dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln --no-restore --filter "FullyQualifiedName~Phase6"
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
dotnet run --project Publishing-Tracker/apps/api/src/PublishingTracker.Api --launch-profile https
```
*(The `https` launch profile listens at `https://localhost:7285;http://localhost:5226` and runs `DevelopmentCatalogSeeder` automatically on startup so `author-a@example.test` / `Password123!` works immediately.)*

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
2. Store secrets with `.NET User Secrets` (recommended for local dev) and start `PublishingTracker.Agent` in terminal 2 with an explicit listen URL (since `PublishingTracker.Agent` has no default `launchSettings.json`):
   ```powershell
   dotnet user-secrets --project Publishing-Tracker/apps/api/src/PublishingTracker.Agent set "AIProvider:Type" "gemini"
   dotnet user-secrets --project Publishing-Tracker/apps/api/src/PublishingTracker.Agent set "AIProvider:GeminiApiKey" "<your-gemini-key>"
   $env:ASPNETCORE_ENVIRONMENT="Development"
   $env:Jwt__Key="<same-signing-key-as-PublishingTracker.Api>"
   $env:Jwt__Issuer="PublishingTracker.Api"
   $env:Jwt__Audience="PublishingTracker.Api"
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

## 8. Phase 4 RAG Test Matrix (`16/16` Implemented & Passing)

Specified in [Phase-4-RAG-Module-with-pgvector.md](Phase-4-RAG-Module-with-pgvector.md) (`Phase4RagRetrievalAndIsolationTests.cs`, offline via `DeterministicTestEmbeddingGenerator` and `TestWebAppFactory`/EF Core doubles):

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
11. `RagSearchTool_InvalidQueryOrTopKArguments_FailsWithStructuredError` *(Step 4.6)* — Verifies pre-call tool argument checks reject missing/whitespace/oversized `query` and invalid `topK` with structured `INVALID_TOOL_ARGUMENTS`.
12. `EvidenceLedger_IndexesSearchPublishingGuidelinesOutput_AndFailsClosedOnMalformedPayload` *(Step 4.6)* — Verifies two-pass RAG hit indexing, prefix validation (`{docId}#c{idx}`), duplicate rejection, and fail-closed handling on corrupt payloads.
13. `Stage2_CombinedCatalogueAndDocumentCitations_VerifiedAgainstEvidenceLedger_Succeeds` *(Step 4.6)* — Verifies corroborated catalogue and document citations succeed together, while unreturned chunks/fields are rejected.
14. `Stage2_StaleOrUnreturnedDocumentChunkCitation_RejectedAndRepairedOnRetry` *(Step 4.6)* — Verifies unreturned chunk citations and case mismatches fail closed.
15. `PromptInjection_InRetrievedChunk_RequestingUnknownToolOrFabricatedCitation_IsBlockedByHostGuards` *(Step 4.6)* — Verifies injected directives attempting to invoke unapproved tools or fabricate citations are blocked by host guards (`UNKNOWN_TOOL:`).
16. `RagSearchTool_EmptyHitsSearch_BlocksUncitedAnsweredDraft_EvenWhenCatalogueSearchWasEmpty` *(Step 4.6)* — Verifies empty RAG search prevents uncited `"answered"` status, requiring evidence citations whenever guideline searches occurred.

---

## 9. Phase 5 Layered Evaluation Suite (`PublishingTracker.Evals` — 39 Tests)

Specified in [Phase-5-Layered-Evaluation-Strategy.md](Phase-5-Layered-Evaluation-Strategy.md), the `PublishingTracker.Evals` project provides a continuous, multi-tiered verification harness across three layers:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        PublishingTracker.Evals                         │
├────────────────────────────────────────────────────────────────────────┤
│ Layer 1: Deterministic Benchmarks & Evaluators (30 tests)              │
│   ├── GoldenDatasetTests (12 canonical scenarios across 4 categories)  │
│   ├── TrajectoryEvaluatorTests (tool sequence, forbidden tools, turns) │
│   ├── ProvenanceScorerTests (structural provenance & corpus checks)   │
│   ├── IndirectPromptInjectionTests (injected note UNKNOWN_TOOL guard)  │
│   └── GoldenScenarioEndToEndTests (ScriptedChatClient end-to-end)      │
├────────────────────────────────────────────────────────────────────────┤
│ Layer 2: PostgreSQL & pgvector Integration (4 tests)                   │
│   ├── Real pgvector <=> cosine distance operator parity                │
│   ├── Strict SQL OwnerUserId isolation (Shared vs User 1 vs User 2)    │
│   ├── Concurrent interleaved multi-tenant queries (N=20 parallel)      │
│   └── Mid-run token expiry fail-closed check (port 5435 / env)         │
├────────────────────────────────────────────────────────────────────────┤
│ Layer 3: Opt-In Live Smoke & Baseline Gates (5 tests, Category=LiveEval│
│   ├── Privacy policy & eval_baseline.json regression gate (offline)    │
│   ├── Multi-provider support: Ollama (local), Gemini 2.5 Flash,        │
│   │   OpenRouter                                                       │
│   ├── Fail-fast timeouts (30s per-call, 60s overall)                   │
│   └── RateLimitedRetryHandler (bounded 429 exponential backoff)        │
└────────────────────────────────────────────────────────────────────────┘
```

### 9.1 Layer 1 Deterministic Evaluator Tests (`30` Tests Across 5 Files)
Runs 100% offline with zero external network dependencies or token consumption:
- [GoldenDatasetTests.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Evals/GoldenDatasetTests.cs) (`13` test executions): Verifies all 12 canonical scenarios in [GoldenDataset.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Evals/GoldenDataset.cs) have unique IDs, non-empty prompts, and disjoint expected/forbidden tools.
- [TrajectoryEvaluatorTests.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Evals/TrajectoryEvaluatorTests.cs) (`6` tests): Exercises [TrajectoryEvaluator.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Evals/Evaluators/TrajectoryEvaluator.cs) across expected tools, forbidden tools, answered/failed status checks, and `GoldenTrajectory` turn/call-count drift.
- [ProvenanceScorerTests.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Evals/ProvenanceScorerTests.cs) (`5` tests): Exercises [ProvenanceScorer.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Evals/Evaluators/ProvenanceScorer.cs) validating complete `(bookId, fields, claim)` and `(documentId, chunkId, claim)` structures and rejecting citations outside `ExpectedDocumentIds`.
- [IndirectPromptInjectionTests.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Evals/IndirectPromptInjectionTests.cs) (`2` tests): Proves ingested RAG note payloads requesting unregistered tools (`delete_all_titles`, `export_other_user_data`) fail closed with `UNKNOWN_TOOL:`.
- [GoldenScenarioEndToEndTests.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Evals/GoldenScenarioEndToEndTests.cs) (`4` tests): Runs end-to-end `AgentLoopRunner` trajectories using `ScriptedChatClient` across catalogue lookups, locked-corpus RAG citations, out-of-corpus provenance detection, and unanswerable queries.

### 9.2 Layer 2 PostgreSQL & pgvector Integration Tests (`4` Tests)
Implemented in [AgentMcpIntegrationEvalTests.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Evals/AgentMcpIntegrationEvalTests.cs) with [PostgresTestDatabaseFixture.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Evals/PostgresTestDatabaseFixture.cs) against PostgreSQL equipped with `vector`:
- **Port 5435 Default & Hard-Fail Fixture:** Defaults to `Host=localhost;Port=5435;Database=publishingtracker_dev;Username=postgres;Password=postgres` (to avoid port collision with native Windows PostgreSQL on 5432), overridable via `TEST_POSTGRES_CONNECTION`. Hard-fails if PostgreSQL/`pgvector` is unreachable rather than degrading silently to InMemory.
- **Integration Coverage:**
  - `RealPgvector_OperatorParity_MatchesCSharpCosineDistanceRanking`: Proves SQL `<=>` ordering matches C# `VectorMath.CosineDistance` within precision 3.
  - `RealPgvector_OwnerFilterEnforcedInSql_NotOnlyByQueryFilter`: Verifies User 1 and User 2 receive shared chunks (`OwnerUserId == null`) and only their own private chunks via `IRagChunkStore.SearchByVectorAsync`.
  - `ConcurrentUsers_NoCrossTenantContamination_UnderInterleavedRequests`: Executes `N=20` parallel interleaved queries across User 10 and User 20.
  - `AgentRunner_MidRunTokenExpiry_FailsWithToolError_AndNoSecretLeak`: Confirms mid-run 401 exceptions fail closed with `TOOL_ERROR:` without leaking token strings.

### 9.3 Layer 3 Opt-In Live Smoke Evals ([LiveSmokeEvalTests.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Evals/LiveSmokeEvalTests.cs) — 5 Tests)
Marked with `[Trait("Category", "LiveEval")]`. Two tests (`LiveSmoke_PrivacyPolicy_MandatesZeroRetentionAndProvenanceHeaders` and `LiveSmoke_BaselineScorecard_ValidatesThresholdsAndDetectsRegressions`) validate privacy policy and [eval_baseline.json](../Publishing-Tracker/apps/api/tests/PublishingTracker.Evals/eval_baseline.json) / [BaselineEnforcer.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Evals/Evaluators/BaselineEnforcer.cs) deterministically; the remaining 3 run end-to-end LLM tool-calling and Stage 2 validation when `RUN_LIVE_EVALS=true` and provider credentials are supplied.

#### Supported Providers & Priority
1. **Local Ollama (First Priority if requested):**
   - Configured via `LIVE_EVAL_PROVIDER=ollama` or `OLLAMA_ENDPOINT` (defaults to `http://localhost:11434/v1`).
   - Default model: `qwen2.5-coder:7b` (configurable via `OLLAMA_MODEL_NAME`); embeddings: `nomic-embed-text`.
2. **Google Gemini Flash (Recommended for Online Live Evals):**
   - Configured via `GEMINI_API_KEY`.
   - Default model: `gemini-2.5-flash` (overridable via `LIVE_EVAL_MODEL_ID`) at `https://generativelanguage.googleapis.com/v1beta/openai`.
3. **OpenRouter:**
   - Configured via `LIVE_EVAL_OPENROUTER_API_KEY` or `OPENROUTER_API_KEY`.
   - Default live eval model: `openai/gpt-4o-mini` (overridable via `LIVE_EVAL_MODEL_ID`), with OpenRouter ZDR JSON payload `"provider": { "data_collection": "deny" }` plus privacy headers (`data_collection: deny`, `HTTP-Referer`, `X-Title`).

#### Live Resilience & Guardrails
- **Fail-Fast Timeouts:** Live evals enforce strict per-call timeouts (`30s`) and overall timeouts (`60s`).
- **Bounded 429 Retry Handler:** Nested `RateLimitedRetryHandler` intercepts HTTP 429 responses and applies exponential backoff (up to 3 retries).
- **Parameter Normalization:** Tool invocation normalizes missing pagination arguments (`page=1`, `pageSize=20`) when the model omits optional defaults.
- **Pure Catalogue Citation Discipline:** `Stage2FormattingPrompt` instructs models that `documentSources` must remain empty when no guideline documents were searched.

#### Execution Commands
```powershell
# Run only deterministic & PostgreSQL integration evals (34 tests, default in CI)
dotnet test Publishing-Tracker/apps/api/tests/PublishingTracker.Evals --filter "Category!=LiveEval"

# Run live smoke evals with Gemini Flash (5 tests)
$env:RUN_LIVE_EVALS="true"
$env:GEMINI_API_KEY="<your-gemini-key>"
dotnet test Publishing-Tracker/apps/api/tests/PublishingTracker.Evals --filter "Category=LiveEval"

# Run live smoke evals with local Ollama (5 tests)
$env:RUN_LIVE_EVALS="true"
$env:LIVE_EVAL_PROVIDER="ollama"
dotnet test Publishing-Tracker/apps/api/tests/PublishingTracker.Evals --filter "Category=LiveEval"
```

---

## 10. Phase 6 Deployment, Security & HealthCheck Verification (`25` Tests)

Specified in [Phase-6-Deployment-and-Operational-Readiness.md](Phase-6-Deployment-and-Operational-Readiness.md) and [Deployment-and-Operations-Guide.md](Deployment-and-Operations-Guide.md) ([Phase6DeploymentAndSecurityTests.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Agent.Tests/Phase6DeploymentAndSecurityTests.cs) in `PublishingTracker.Agent.Tests`):

```powershell
# Run the 25 Phase 6 deployment and security tests
dotnet test Publishing-Tracker/apps/api/tests/PublishingTracker.Agent.Tests `
  --filter "FullyQualifiedName~Phase6DeploymentAndSecurityTests"
```

### Verified Scenarios
1. `AIProviderOptions_AllowInsecureInternalHttp_FailsFastOutsideDevelopmentOrTesting`:
   - Verifies that setting `AllowInsecureInternalHttp = true` with `isDevelopmentOrTesting = false` returns a validation error prohibiting insecure internal HTTP in Production/Staging.
2. `AIProviderOptions_AllowInsecureInternalHttp_PermittedInDevelopmentAndTesting`:
   - Verifies that `AllowInsecureInternalHttp = true` is valid in Development and Testing environments when pointing to `http://api:8080/mcp`.
3. `AIProviderOptions_IsEndpointTrusted_EnforcesStrictAllowlist` *(12 theory permutations)*:
   - Proves HTTPS endpoints are always trusted across Development, Testing, and Production.
   - Proves HTTP loopback (`localhost`, `127.0.0.1`) is trusted in Development/Testing, but untrusted in Production.
   - Proves HTTP container hosts (`api`, `publishingtracker-api`) are trusted only when `AllowInsecureInternalHttp = true` AND `isDevelopmentOrTesting = true`.
   - Proves untrusted external HTTP hosts (`evil.attacker.com`), URLs with query strings (`?query=leak`), and URLs with credentials (`user:pass@`) are strictly rejected.
4. `PerRequestBearerHandler_AllowInsecureInternalHttp_PermitsInternalContainerHostInDevelopment`:
   - Verifies outbound requests to `http://api:8080/mcp` succeed and attach the caller's Bearer token in Development when `AllowInsecureInternalHttp = true`.
5. `PerRequestBearerHandler_AllowInsecureInternalHttp_RejectsWhenDisabledOrInProduction`:
   - Verifies that disabling `AllowInsecureInternalHttp` or running in Production throws `InvalidOperationException`.
6. `HealthProbe_Main_ReturnsZeroOnSuccess_AndOneOnFailure`:
   - Verifies that `PublishingTracker.HealthProbe` exits with `1` on invalid target URLs or unreachable endpoints.
7. `JwtKeyValidation_EnforcesMinimum32ByteLength`:
   - Verifies `AIProviderOptions.ValidateJwtKey` rejects null/whitespace keys and keys under 32 UTF-8 bytes while accepting 32+ byte keys.
8. `HealthProbe_Main_ReturnsZeroAgainstHealthyServer_AndOneAgainstUnhealthyServer`:
   - Exercises `PublishingTracker.HealthProbe` against a live loopback HTTP server returning `200 OK` (exit `0`) vs `503 Service Unavailable` (exit `1`).
9. `AIProviderSetup_Resolve_SupportsGeminiProviderWithEnvironmentVariables`:
   - Verifies first-class `AIProvider:Type="gemini"` resolution via `GEMINI_API_KEY`, `GOOGLE_API_KEY`, and `GEMINI_MODEL_NAME`.
10. `DevelopmentCatalogSeeder_IsIdempotent_AndSeedsAuthenticatableDemoUsersAndBooks`:
    - Verifies `DevelopmentCatalogSeeder.SeedAsync` is idempotent, hashes `Password123!` using BCrypt SHA-256 compatible with `/api/auth/login`, and seeds the 6 canonical books + 6 RAG chunks across User A and User B.
11. `ContainerSecretsConfiguration_LoadsCanonicalAndKeyPerFileSecrets_AndTrimsNewlines`:
    - Verifies `/run/secrets/` file loading trims trailing `\r\n`, ignores `.hidden` and whitespace-only files, and maps both canonical Compose secret names (`ai_provider_api_key`, `gemini_api_key.secret`, `jwt_key.txt`) and `AIProvider__OpenRouterApiKey` ASP.NET Core keys.
12. `AIProviderSetup_Resolve_SupportsFileMountedSecretsAndFileEnvVars_WithoutPlaintextEnvVars`:
    - Verifies hosted provider resolution reads `GEMINI_API_KEY_FILE`, `OPENROUTER_API_KEY_FILE`, and `/run/secrets/ai_provider_api_key` without any plaintext API key environment variables.
13. `Assemblies_ConfigureUserSecretsId_ForLocalDevelopment`:
    - Verifies `<UserSecretsId>` assembly attribute metadata is present on `PublishingTracker.Agent` for `dotnet user-secrets` workflows.
14. `AppSettingsJson_ContainsNoCommittedCredentialsOrJwtSigningKeys`:
    - Verifies committed [appsettings.json](../Publishing-Tracker/apps/api/src/PublishingTracker.Api/appsettings.json) contains blank `ConnectionStrings` (`DefaultConnection`, `NeonConnection`, and no `AzureConnection` secret) and blank `Jwt:Key`.
