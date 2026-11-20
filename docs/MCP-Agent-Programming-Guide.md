# PublishingTracker MCP & Agent — Programming Guide

Developer reference and walkthrough of the **PublishingTracker MCP Server**, **PublishingTracker.Agent**, and **PublishingTracker.Rag** implementation across **Phases 0–4 (Complete)**. Companion to [MCP-User-Guide.md](MCP-User-Guide.md), [MCP-Testing-Guide.md](MCP-Testing-Guide.md), and [MCP-Agent-Design-Log.md](MCP-Agent-Design-Log.md).

---

## 1. Architecture & Solution Map

All MCP, Agent, and RAG code lives inside the `.NET 10` solution at [PublishingTracker.Api.sln](../Publishing-Tracker/apps/api/PublishingTracker.Api.sln):

```text
Publishing-Tracker/apps/api/
  src/
    PublishingTracker.Domain/           # Core Book / Edition entities & invariants
    PublishingTracker.Application/      # FormatCompletenessRules & MediatR catalogue queries
    PublishingTracker.Infrastructure/   # PostgreSQL EF Core persistence, IRagChunkStore & JWT TokenService
    PublishingTracker.Mcp/              # [McpServerToolType] read-only MCP tools
    PublishingTracker.Rag/              # VectorMath, MarkdownChunker, Ingestion, Retrieval, RagSearchToolFactory
    PublishingTracker.Api/              # REST endpoints + hosted Streamable HTTP /mcp endpoint
    PublishingTracker.Agent/            # ASP.NET Core Agent Host, IChatClient loop, CompositeToolProvider, EvidenceLedger
  tests/
    PublishingTracker.Api.Tests/        # Phase 0 baseline + Phase 1 API & Streamable HTTP MCP integration tests (54 tests)
    PublishingTracker.Agent.Tests/      # Phases 2–4 loop, auth, provider, evidence, validation & RAG tests (58 tests)
```

| Project | Role | Key Entry Points |
|---|---|---|
| `PublishingTracker.Domain` | Book, Edition, and Platform metadata entities | [Book.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Domain/Entities/Book.cs), [Edition.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Domain/Entities/Edition.cs) |
| `PublishingTracker.Application` | Format completeness rules and owner-scoped CQRS queries | [FormatCompletenessRules.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Application/Books/Services/FormatCompletenessRules.cs), [FindTitlesMissingMetadataQuery.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Application/Books/Queries/FindTitlesMissingMetadataQuery.cs), [GetTitleDetailsQuery.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Application/Books/Queries/GetTitleDetailsQuery.cs) |
| `PublishingTracker.Infrastructure` | `Npgsql` DbContext, `RagDocumentChunk` store, and JWT generation | [TokenService.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Infrastructure/Authentication/TokenService.cs), [PublishingTrackerDbContext.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Infrastructure/Persistence/PublishingTrackerDbContext.cs) |
| `PublishingTracker.Mcp` | Read-only MCP tool definitions backed by `MediatR` | [CatalogueMcpTools.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Mcp/Tools/CatalogueMcpTools.cs) |
| `PublishingTracker.Rag` | Vector math, markdown chunking, ingestion, and host-bound search tool | [VectorMath.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Rag/Services/VectorMath.cs), [MarkdownChunker.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Rag/Services/MarkdownChunker.cs), [RagIngestionService.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Rag/Services/RagIngestionService.cs), [RagRetrievalService.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Rag/Services/RagRetrievalService.cs), [RagSearchToolFactory.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Rag/Tools/RagSearchToolFactory.cs) |
| `PublishingTracker.Api` | Maps `/api/books/*` REST routes and `/mcp` MCP transport | [Program.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Api/Program.cs), [BookEndpoints.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Api/Endpoints/BookEndpoints.cs) |
| `PublishingTracker.Agent` | Authenticated two-stage agent orchestrator, composite tool provider & output verification | [Program.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Program.cs), [AgentLoopRunner.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Orchestration/AgentLoopRunner.cs), [CompositeToolProvider.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Mcp/CompositeToolProvider.cs), [EvidenceLedger.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Orchestration/EvidenceLedger.cs), [AgentOutputValidator.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Validation/AgentOutputValidator.cs) |

---

## 2. Phase 0 & Phase 1 — Domain, Auth, CQRS & MCP Functions

### 2.1 Domain Entities (`PublishingTracker.Domain`)

| Symbol & Signature | File Location | Purpose |
|---|---|---|
| `Book.AddEdition(EditionFormat format, string? isbn = null, DateTime? publicationDate = null, decimal? basePrice = null, int? pageCount = null, string? dimensions = null, decimal? fileSize = null, string? narrator = null, TimeSpan? duration = null) -> Edition` | [Book.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Domain/Entities/Book.cs#L80-L98) | Creates and attaches an `Edition`, propagating audiobook `narrator` and `duration` fields that were previously dropped. |
| `Edition.Update(string? isbn, DateTime? publicationDate, decimal? basePrice, int? pageCount = null, string? dimensions = null, decimal? fileSize = null, string? narrator = null, TimeSpan? duration = null)` | [Edition.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Domain/Entities/Edition.cs#L58-L70) | Updates edition attributes and normalizes `PublicationDate` via `NormalizeUtc`. |
| `Edition.NormalizeUtc(DateTime? value) -> DateTime?` *(private)* | [Edition.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Domain/Entities/Edition.cs#L72-L78) | Ensures `PublicationDate` values use `DateTimeKind.Utc` before PostgreSQL `timestamptz` persistence. |

### 2.2 JWT Hygiene (`PublishingTracker.Infrastructure`)

| Symbol & Signature | File Location | Purpose |
|---|---|---|
| `TokenService.CreateToken(User user) -> string` | [TokenService.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Infrastructure/Authentication/TokenService.cs#L19-L49) | Issues HMAC-SHA256 JWTs containing an integer `JwtRegisteredClaimNames.Iat` claim and UTC expiry (`Jwt:ExpiryMinutes`, default `60`). |

### 2.3 Format Completeness & Catalogue Queries (`PublishingTracker.Application`)

#### [FormatCompletenessRules.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Application/Books/Services/FormatCompletenessRules.cs)
- `EvaluateBook(Book book) -> BookCompletenessEvaluation`: Evaluates book-level metadata (`Description`, `Genre`, `CoverImageUrl`, non-empty `Editions`, and `SeriesNumber` when `SeriesName` is present) plus each child edition ordered by `Id`.
- `EvaluateEdition(Edition edition) -> EditionCompletenessEvaluation`: Applies format-specific rules:
  - All formats: `PublicationDate`, `BasePrice` (`>= 0m` valid, `null` missing).
  - Print (`Paperback`, `Hardcover`, `LargeText`): `ISBN`, `PageCount` (`> 0`), `Dimensions`.
  - `Ebook`: `ISBN_Or_PlatformProductId`, `FileSize` (`> 0`).
  - `Audiobook`: `ISBN_Or_PlatformProductId`, `Narrator`, `Duration` (`> TimeSpan.Zero`).
- Private helpers: `HasIsbnOrPlatformProductId(Edition edition) -> bool`, `IsMissing(string? value) -> bool`.
- Supporting records: `EditionCompletenessEvaluation`, `BookCompletenessEvaluation` (with `IsIncomplete`).

#### [FindTitlesMissingMetadataQuery.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Application/Books/Queries/FindTitlesMissingMetadataQuery.cs)
- `FindTitlesMissingMetadataQueryHandler.Handle(FindTitlesMissingMetadataQuery request, CancellationToken cancellationToken) -> Task<Result<MissingMetadataPageDto>>`: Requires non-null authenticated `UserId`, validates `page >= 1`, `1 <= pageSize <= 50`, declared `EditionFormat` names, and known completeness fields, then returns bounded pages (`TotalCount`, `HasMore`, `Items`).
- `FindTitlesMissingMetadataQueryHandler.Project(BookCompletenessEvaluation eval, EditionFormat? format, string? missingField) -> MissingTitleItemDto?` *(private)*: Applies `format` and `missingField` filters; format filters scope to edition gaps only.
- Contracts: `FindTitlesMissingMetadataQuery`, `MissingMetadataPageDto`, `MissingTitleItemDto`, `MissingEditionFieldsDto`.

#### [GetTitleDetailsQuery.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Application/Books/Queries/GetTitleDetailsQuery.cs)
- `GetTitleDetailsQueryHandler.Handle(GetTitleDetailsQuery request, CancellationToken cancellationToken) -> Task<Result<TitleDetailsDto>>`: Loads full `Book`, `Editions`, completeness flags, and `PlatformSummaries` for `request.BookId` owned by `_currentUserService.UserId`. Returns an identical `NOT_FOUND_OR_UNAUTHORIZED` error whether the ID is missing or belongs to another user.
- Contracts: `GetTitleDetailsQuery`, `TitleDetailsDto`, `EditionDetailsDto`, `PlatformSummaryDto`.

### 2.4 MCP Server Tools & REST Endpoints (`PublishingTracker.Mcp` & `PublishingTracker.Api`)

| Symbol / Route | File Location | Purpose |
|---|---|---|
| `CatalogueMcpTools.FindTitlesMissingMetadataAsync(string? format = null, string? missingField = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) -> Task<MissingMetadataPageDto>` | [CatalogueMcpTools.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Mcp/Tools/CatalogueMcpTools.cs#L12-L37) | `[McpServerTool(Name = "find_titles_missing_metadata", UseStructuredContent = true, ReadOnly = true, Idempotent = true, Destructive = false)]` wrapper dispatching `FindTitlesMissingMetadataQuery`. Throws `McpException` on failure. |
| `CatalogueMcpTools.GetTitleDetailsAsync(int bookId, CancellationToken cancellationToken = default) -> Task<TitleDetailsDto>` | [CatalogueMcpTools.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Mcp/Tools/CatalogueMcpTools.cs#L39-L56) | `[McpServerTool(Name = "get_title_details", UseStructuredContent = true, ReadOnly = true, Idempotent = true, Destructive = false)]` wrapper dispatching `GetTitleDetailsQuery`. |
| `BookEndpoints.MapBookEndpoints(this WebApplication app)` (`GET /api/books/missing-metadata`, `GET /api/books/{id:int}/details`) | [BookEndpoints.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Api/Endpoints/BookEndpoints.cs#L43-L62) | Authenticated REST equivalents sharing the exact CQRS handlers. |
| `BookEndpoints.MapCatalogueError(string? message) -> IResult` *(private)* | [BookEndpoints.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Api/Endpoints/BookEndpoints.cs#L120-L126) | Maps `UNAUTHORIZED:` to 401, `NOT_FOUND_OR_UNAUTHORIZED:` to 404, and validation errors to 400. |
| `AddMcpServer().WithHttpTransport(options => options.Stateless = true).WithTools<CatalogueMcpTools>()` & `app.MapMcp("/mcp").RequireAuthorization()` | [Program.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Api/Program.cs#L84-L86), [L195](../Publishing-Tracker/apps/api/src/PublishingTracker.Api/Program.cs#L195) | Hosts stateless Streamable HTTP MCP transport inside the API after `UseAuthentication()`, `UseAuthorization()`, and `UseRoleValidation()`. |

---

## 3. Phase 2 — Agent Host Setup, Auth Forwarding, MCP Client & Telemetry

### 3.1 AI Provider & Loop Configuration (`PublishingTracker.Agent.Configuration`)

| Symbol & Signature | File Location | Purpose |
|---|---|---|
| `AIProviderOptions.Validate() -> string?` | [AIProviderOptions.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Configuration/AIProviderOptions.cs#L23-L48) | Case-insensitively enforces supported `Type` (`openrouter`, `openai`, `ollama`), HTTPS for non-loopback endpoints, required API keys for hosted providers, and clean `/mcp` URI structure. |
| `AIProviderOptions.ToString() -> string` / `Redact(string? value) -> string` | [AIProviderOptions.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Configuration/AIProviderOptions.cs#L50-L53) | Formats provider settings for logs while replacing `ApiKey` and `OpenRouterApiKey` with `[REDACTED]` (`[JsonIgnore]` also blocks JSON serialization). |
| `AIProviderSetup.Resolve(AIProviderOptions configured, Func<string, string?> getEnv) -> AIProviderOptions` | [AIProviderSetup.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Configuration/AIProviderSetup.cs#L17-L61) | Validates `AIProvider:Type` strictly and resolves endpoint, model, and API key fallbacks from configuration and environment variables. |
| `AIProviderSetup.CreateChatClient(AIProviderOptions options, HttpClient? customHttpClient = null) -> IChatClient` | [AIProviderSetup.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Configuration/AIProviderSetup.cs#L63-L94) | Constructs an OpenAI-compatible `IChatClient`, wires `HttpClientPipelineTransport(customHttpClient)` for tests, attaches `OpenRouterHeadersPolicy` (case-insensitively for `"openrouter"`), and wraps the pipeline with `.AsBuilder().UseOpenTelemetry(sourceName: AgentTelemetry.ActivitySourceName, configure: telemetry => telemetry.EnableSensitiveData = false).Build()`. |
| `OpenRouterHeadersPolicy.Process` / `ProcessAsync` / `ApplyHeaders` *(private)* | [AIProviderSetup.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Configuration/AIProviderSetup.cs#L104-L124) | Attaches `HTTP-Referer`, `X-Title: PublishingTracker`, and `data_collection: deny` per outbound OpenRouter call. |
| `AgentLoopOptions.Validate() -> void` | [AgentLoopOptions.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Configuration/AgentLoopOptions.cs#L15-L25) | Rejects non-positive limits (`MaxModelTurns = 6`, `MaxTotalToolCalls = 8`, `RepeatCallLimit = 2`, `MaxToolPayloadBytes = 16_384`, `MaxPaginationPages = 2`), negative `MaxValidationRetries` (default `2`), non-positive `PerCallTimeout` (`20s`), and `OverallTimeout <= PerCallTimeout` (`60s`). |

### 3.2 Per-Request JWT Forwarding & MCP Tool Discovery

| Symbol & Signature | File Location | Purpose |
|---|---|---|
| `PerRequestBearerHandler.SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) -> Task<HttpResponseMessage>` | [PerRequestBearerHandler.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Auth/PerRequestBearerHandler.cs#L12-L37) | Validates the destination MCP URI, requires `Authorization.Count == 1` with a non-empty whitespace-free Bearer token from `IHttpContextAccessor`, strips any pre-existing header, and sets `new AuthenticationHeaderValue("Bearer", token)` per outbound `HttpRequestMessage`. |
| `PerRequestBearerHandler.ValidateDestination(Uri? actual) -> void` *(private)* | [PerRequestBearerHandler.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Auth/PerRequestBearerHandler.cs#L39-L60) | Verifies exact scheme/host/port/`/mcp` path match against `AIProviderOptions.McpEndpoint` and forbids non-loopback HTTP outside `Development`/`Testing`. |
| `IMcpToolProvider.OpenSessionAsync(CancellationToken cancellationToken = default) -> Task<IMcpToolSession>` | [IMcpToolProvider.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Mcp/IMcpToolProvider.cs#L5-L13) | Abstraction for per-request MCP session lifecycle and tool discovery. |
| `StreamableHttpMcpToolProvider.OpenSessionAsync(CancellationToken cancellationToken = default) -> Task<IMcpToolSession>` | [StreamableHttpMcpToolProvider.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Mcp/StreamableHttpMcpToolProvider.cs#L12-L40) | Opens an `HttpClientTransport` (`HttpTransportMode.StreamableHttp`) with `ownsHttpClient: false`, discovers tools via `McpClient.ListToolsAsync`, and returns an `IAsyncDisposable` `McpToolSession`. |
| `AgentTelemetry.ActivitySourceName` & `AgentTelemetry.Source` | [AgentTelemetry.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Telemetry/AgentTelemetry.cs#L5-L9) | Exposes `ActivitySource("PublishingTracker.Agent", "1.0.0")` for `agent.run`, `agent.model_turn`, and `agent.tool_call` spans. |

---

## 4. Phase 3 — Two-Stage Orchestration, Evidence Ledger & Strict Output Verification

### 4.1 Response & Draft Contracts (`PublishingTracker.Agent.Contracts`)

| Symbol & Signature | File Location | Purpose |
|---|---|---|
| `AgentErrorCode` & `AgentErrorCatalog.GetPublicMessage(AgentErrorCode code) -> string` | [AgentContracts.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Contracts/AgentContracts.cs#L5-L56) | Maps 18 stable orchestration failure codes (`CANCELLED`, `TIMEOUT`, `PAGINATION_SEQUENCE_ERROR`, `EVIDENCE_INDEXING_ERROR`, `VALIDATION_RETRIES_EXHAUSTED`, etc.) to privacy-safe public error strings without leaking internal exceptions. |
| `CatalogueSourceCitation`, `DocumentSourceCitation`, `AgentEvidenceDraft`, `AgentModelDraft` | [AgentContracts.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Contracts/AgentContracts.cs#L58-L81) | JSON schema contracts annotated with `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]`. |
| `ToolExecutionRecord(string ToolName, int Turn, long DurationMs, bool Succeeded)` | [AgentContracts.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Contracts/AgentContracts.cs#L83-L87) | Host-recorded execution telemetry per tool call (never supplied by the model). |
| `AgentQueryRequest(string Question)` | [AgentQueryRequest.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Contracts/AgentQueryRequest.cs#L3) | Request body DTO for `POST /api/agent/query`. |
| `AgentQueryResponse.Answered(AgentModelDraft draft, bool hasPaginationTruncation, IReadOnlyList<ToolExecutionRecord> toolsUsed, int turns, int totalToolCalls) -> AgentQueryResponse` | [AgentQueryResponse.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Contracts/AgentQueryResponse.cs#L18-L45) | Builds the `1.0` response envelope from a validated `AgentModelDraft`, injecting `TruncationDisclosureNote` (`hasMore=true`) when pagination truncation occurred. |
| `AgentQueryResponse.Failure(AgentErrorCode errorCode, IReadOnlyList<ToolExecutionRecord> toolsUsed, int turns, int totalToolCalls, bool hasPaginationTruncation = false) -> AgentQueryResponse` | [AgentQueryResponse.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Contracts/AgentQueryResponse.cs#L47-L62) | Builds a `"could_not_answer"` envelope with an approved `AgentErrorCatalog` message and host-recorded tool history. |

### 4.2 Per-Run Evidence Ledger ([EvidenceLedger.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Orchestration/EvidenceLedger.cs))

| Method / Helper | Purpose |
|---|---|
| `RecordToolExecution(string toolName, int turn, long durationMs, bool succeeded)` | Appends a host-observed `ToolExecutionRecord` to `ToolsUsed`. |
| `TryValidatePaginationRequest(string toolName, IDictionary<string, object?>? arguments, int maxPaginationPages, out AgentErrorCode errorCode, out string diagnosticError) -> bool` | Pre-validates `find_titles_missing_metadata` calls: rejects invalid `page` / `pageSize` (`1..50`), total page budget overflow (`> maxPaginationPages`), skipped starting pages (`page != 1`), non-sequential pages, mid-query `pageSize` changes, or paging past `hasMore == false`. |
| `TryRecordToolOutput(string toolName, IDictionary<string, object?>? arguments, object? rawResult, string serializedJson, out AgentErrorCode errorCode, out string diagnosticError) -> bool` | Unwraps and indexes `find_titles_missing_metadata` (`MissingMetadataPageDto`) and `get_title_details` (`TitleDetailsDto`) outputs (verifying requested vs. returned `bookId`, validating `PlatformSummaryDto` entries, and indexing `TitleDetailsPlatformSummaryFields` only when `PlatformSummaries.Count > 0`); fails closed with `EVIDENCE_INDEXING_ERROR` or `TOOL_ERROR` if unrecognized or inconsistent. |
| `TryValidateCatalogueCitation(int bookId, int? editionId, IReadOnlyList<string> fields, out string error) -> bool` | Checks that `(bookId, editionId?)` exists in the ledger and every cited field name (trimmed symmetrically with `AddCatalogueFields`) was returned for that linked entity (when `editionId` is set, merges edition fields with parent book fields). |
| `CanAuthorizeZeroCitationAnswer(string answerText, out string error) -> bool` | Permits zero citations only when the run executed a single `find_titles_missing_metadata` search with `totalCount == 0` and no items/documents recorded, and the answer references the active `format` / `missingField` filter(s) (matching exact identifiers or humanized multi-word names via `AnswerMentionsFilter` / `HumanizeIdentifier`). |
| `RecordDocumentHit(string documentId, string chunkId, string excerpt)` & `HasDocumentEvidence(string documentId, string chunkId) -> bool` | Indexes ([L159-L165](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Orchestration/EvidenceLedger.cs#L159-L165)) and verifies `(documentId, chunkId)` document evidence pairs (extension point wired for Phase 4 RAG). |
| *Internal helpers & field sets:* `TitleDetailsPlatformSummaryFields`, `TryIndexMissingMetadataPage`, `TryIndexTitleDetails`, `TryUnwrapMissingMetadataPage`, `TryUnwrapTitleDetails`, `TryDeserializePayload<T>`, `TryExtractPayloadElement`, `AddCatalogueFields`, `VerifyFieldsExist`, `AnswerMentionsFilter`, `HumanizeIdentifier`, `BuildMissingMetadataFilterKey`, `TryExtractIntArgument` | Handle `CallToolResult` (`isError`, `structuredContent`, `content[0].text`) unwrapping, strict `totalCount`/`items.Count`/`hasMore` consistency math, humanized filter matching, and per-filter state keys (`PaginationQueryState`, `CatalogueSearchRecord`). |

### 4.3 Strict Output Validator ([AgentOutputValidator.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Validation/AgentOutputValidator.cs))

| Symbol & Signature | Purpose |
|---|---|
| `AgentOutputValidator.Validate(string? rawJson, EvidenceLedger ledger) -> AgentValidationResult` | Enforces `MaxDraftBytes` (`32_768` UTF-8 bytes), duplicate-key rejection, strict deserialization (`UnmappedMemberHandling.Disallow`, no comments/trailing commas), allowed `Status`, non-empty `Answer` and `Claim`s, linked `EvidenceLedger` checks for every catalogue/document citation, zero-citation gating, and mandatory truncation markers (`TruncationDisclosureMarkers`) when `ledger.HasPaginationTruncation` is `true`. |
| `TryValidateStrictJsonNoDuplicateKeys(byte[] utf8Bytes, out string error) -> bool` *(private)* | Uses `Utf8JsonReader` with per-object `HashSet<string>(OrdinalIgnoreCase)` scopes to fail closed before `JsonSerializer` would silently overwrite duplicate keys. |
| `AgentValidationResult.Success(AgentModelDraft draft)` / `Fail(string error)` | Represents validation pass/fail outcomes returned to `ExecuteStage2Async`. |

### 4.4 Prompts & Two-Stage Loop Runner (`PublishingTracker.Agent.Orchestration`)

#### [AgentPrompts.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Orchestration/AgentPrompts.cs)
- `SystemPromptV1`: Base system instruction establishing read-only tool usage, untrusted-data boundaries, and PII restrictions.
- `Stage2FormattingPrompt`: Instructs the model to format Stage 2 findings strictly as `AgentModelDraft` JSON without calling tools or inventing unlinked IDs/properties.
- `BuildStage2RepairPrompt(string validationError) -> string`: Builds the bounded repair instruction containing the specific validation error.

#### [AgentLoopRunner.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Orchestration/AgentLoopRunner.cs) & [AgentRunResult.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Orchestration/AgentRunResult.cs)
- `RunVerifiedAsync(string userPrompt, CancellationToken cancellationToken = default) -> Task<AgentQueryResponse>`: **Phase 3 primary entrypoint** used by `POST /api/agent/query`. Runs Stage 1 in strict evidence mode followed by Stage 2 structured output and repair validation.
- `RunAsync(string userPrompt, CancellationToken cancellationToken = default) -> Task<AgentRunResult>`: **Phase 2 entrypoint** retained so Phase 2 loop tests continue verifying Stage 1 single-stage behavior cleanly (`AgentRunResult.Completed` / `AgentRunResult.Failed`).
- `ExecuteStage1Async(string userPrompt, bool strictEvidenceMode, CancellationToken cancellationToken) -> Task<LoopRunState>` *(private)*: Opens an `IMcpToolSession`, checks for duplicate tool names (`INVALID_TOOLSET`), tags `agent.prompt_version = "v1"` on `agent.run`, runs tool-selection `IChatClient` turns without `ResponseFormat`, reserves Stage 2's turn budget when `strictEvidenceMode` is enabled (`maxStage1Turns = Math.Max(1, _options.MaxModelTurns - 1)`), enforces `MaxTotalToolCalls`, `PerCallTimeout`, `OverallTimeout`, `MaxToolPayloadBytes`, `RepeatCallLimit` via `CanonicalizeSignature`, non-cooperative tool cancellation (`Task.WhenAny` with explicit `toolCts.Cancel()` on timeout), pagination/evidence indexing, and failure-path `agent.duration_ms` / `agent.is_error` / `agent.error_code` telemetry tags.
- `ExecuteStage2Async(LoopRunState state, CancellationToken cancellationToken) -> Task<AgentQueryResponse>` *(private)*: Appends `Stage2FormattingPrompt`, configures `ChatResponseFormat.ForJsonSchema<AgentModelDraft>(JsonSerializerOptions.Web)` with `Tools = null`, enforces `PerCallTimeout` on every formatting/repair call, shares `MaxModelTurns` budget, preserves alternating `ChatRole.User -> ChatRole.Assistant -> ChatRole.User` history on retries (`<= MaxValidationRetries`), and emits Stage 2 telemetry tags (`agent.stage`, `agent.validation_attempt`, `agent.validation_passed`, `agent.validation_retries_used`, and `agent.duration_ms` / `agent.error_code` on failures).
- `CanonicalizeSignature(string name, IDictionary<string, object?>? arguments) -> string` (`WriteCanonical`, `WriteCanonicalElement`) *(private)*: Produces deterministic sorted-key canonical JSON for repeat-call detection across nested objects and primitives.
- `LoopRunState : IAsyncDisposable` *(private)*: Owns `EvidenceLedger`, `Stopwatch`, root `Activity`, linked `CancellationTokenSource`, and `IMcpToolSession` cleanup via `DisposeAsync()`.

### 4.5 Agent Host Startup & Endpoint ([Program.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Program.cs))

| Endpoint / Helper | Location | Purpose |
|---|---|---|
| `JwtBearerEvents.OnTokenValidated` | [Program.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Program.cs#L49-L68) | Rejects validated JWTs missing a user identity claim (`NameIdentifier` / `NameId` / `Sub`) or a positive integer `iat` claim before running any LLM turn. |
| `POST /api/agent/query` (`.RequireAuthorization()`) | [Program.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Program.cs#L86-L93) | Rejects blank `Question` with 400 Bad Request, invokes `runner.RunVerifiedAsync(request.Question, cancellationToken)`, and returns `200 OK` with `AgentQueryResponse`. |
| `IsValid(AgentLoopOptions options) -> bool` | [Program.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Program.cs#L97-L108) | Startup validator backing `AddOptions<AgentLoopOptions>().Validate(...).ValidateOnStart()`. |
| `IsMcpEndpointSecure(AIProviderOptions options, IHostEnvironment environment) -> bool` | [Program.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Program.cs#L110-L117) | Enforces HTTPS for MCP endpoints, allowing loopback HTTP only in `Development` or `Testing`. |
| `Copy(AIProviderOptions from, AIProviderOptions to) -> void` | [Program.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Program.cs#L119-L131) | Copies `AIProviderSetup.Resolve` outputs back onto the bound DI `AIProviderOptions` instance. |

---

## 5. End-to-End Request Execution Flow (Phase 3)

```text
Caller (JWT Bearer)
  │  POST /api/agent/query { "question": "..." }
  ▼
PublishingTracker.Agent (Program.cs)
  │  runner.RunVerifiedAsync(question, cancellationToken)
  ├─► Stage 1: ExecuteStage1Async(..., strictEvidenceMode: true)
  │     ├─ StreamableHttpMcpToolProvider.OpenSessionAsync()
  │     │    └─ PerRequestBearerHandler.SendAsync() -> POST /mcp (CatalogueMcpTools)
  │     ├─ IChatClient.GetResponseAsync(messages, tools)        [No ResponseFormat]
  │     ├─ EvidenceLedger.TryValidatePaginationRequest(...)     [Pre-call paging check]
  │     ├─ tool.InvokeAsync(args) with Task.WhenAny timeouts
  │     └─ EvidenceLedger.TryRecordToolOutput(...)              [Unwraps & indexes fields]
  │
  └─► Stage 2: ExecuteStage2Async(state)
        ├─ Append ChatRole.User (AgentPrompts.Stage2FormattingPrompt)
        ├─ IChatClient.GetResponseAsync(messages, schemaOptions) [Tools = null, ForJsonSchema<AgentModelDraft>]
        ├─ AgentOutputValidator.Validate(response.Text, ledger)
        └─ On failure & retries remaining:
             ├─ Append ChatRole.Assistant (invalid JSON draft)
             └─ Append ChatRole.User (AgentPrompts.BuildStage2RepairPrompt)
```

---

## 6. Test Suites & Verification Doubles

| File | Location | Coverage |
|---|---|---|
| **Two-User Seed Catalog** | [TwoUserSeedCatalog.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Api.Tests/TwoUserSeedCatalog.cs) | `SeedAsync`: Deterministic User A / User B titles covering complete books, free promo ebooks, incomplete audiobooks/series/editions, and cross-tenant boundaries. |
| **Phase 0 Baseline Tests** | [Phase0BaselineTests.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Api.Tests/Phase0BaselineTests.cs) | `TokenService` `iat` + expiry checks and `FormatCompletenessRules` unit checks. |
| **Phase 1 MCP & API Integration** | [Phase1McpAndCatalogueTests.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Api.Tests/Phase1McpAndCatalogueTests.cs) | Live `/mcp` Streamable HTTP client tests: 401 enforcement, schema inspection (no `userId`), filters, `hasMore` pagination, two-user isolation masking, invalid args, and REST parity. |
| **Scripted Chat Double** | [ScriptedChatClient.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Agent.Tests/Doubles/ScriptedChatClient.cs) | `EnqueueFunctionCalls`, `EnqueueText`, `Enqueue`, and `GetResponseAsync` recording `Invocations` for deterministic unit tests. |
| **HTTP Recording Double** | [RecordingHttpMessageHandler.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Agent.Tests/Doubles/RecordingHttpMessageHandler.cs) | `SendAsync` captures outbound provider `HttpRequestMessage`s and headers offline. |
| **Stub MCP Tool Provider** | [StubToolProvider.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Agent.Tests/Doubles/StubToolProvider.cs) | `OpenSessionAsync` and `CreateTool` factory for deterministic tool stubs. |
| **Phase 2 Agent & Provider Tests** | [AgentLoopTests.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Agent.Tests/AgentLoopTests.cs) | 20 unit tests covering dual budgets, timeouts (including `toolToken.IsCancellationRequested`), canonical repeat-call detection, OpenTelemetry span privacy (`agent.prompt_version == "v1"`), `PerRequestBearerHandler` (rejecting multi-header smuggling & whitespace tokens), case-insensitive `AIProviderOptions.Validate()`, `AIProviderSetup_RoutingAndHeaderTests`, and `AIProviderOptions_ToString_RedactsApiKeys`. |
| **Phase 3 Evidence & Validation Tests** | [Phase3EvidenceAndValidationTests.cs](../Publishing-Tracker/apps/api/tests/PublishingTracker.Agent.Tests/Phase3EvidenceAndValidationTests.cs) | 22 unit tests covering linked `EvidenceLedger` indexing (conditional `PlatformSummaries` fields, `get_title_details` `bookId` check, trimmed field citations), `CallToolResult` unwrapping, pagination sequence/pageSize invariants, scoped empty-search isolation (including humanized `"series number"`), strict JSON/byte bounds, Stage 1 turn-5 reservation, Stage 2 repair conversation ordering, and failure envelopes. |

---

## 7. Extension Points & Implemented Phase 4 Symbols (`PublishingTracker.Rag`)

Phase 4 introduces semantic retrieval and composite tool provision (`Steps 4.1`–`4.5` implemented, `Step 4.6` pending):

| Symbol / Function | Module / File Location | Status / Responsibility |
|---|---|---|
| `RagDocumentChunk` & `IRagChunkStore` | [RagDocumentChunk.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Rag/Entities/RagDocumentChunk.cs) & [IRagChunkStore.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Rag/Persistence/IRagChunkStore.cs) | ✅ Implemented (Step 4.1). Portable entity + acyclic store contract implemented by `PublishingTrackerDbContext` (with defense-in-depth `HasQueryFilter`). |
| `RagChunkHitDto`, `RagSearchResultDto` | [RagContracts.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Rag/Contracts/RagContracts.cs) | ✅ Implemented (Step 4.1). Structured return envelope for `search_publishing_guidelines`. |
| `VectorMath` (`ValidateEmbedding`, `CosineDistance`, `FormatPgVectorLiteral`) | [VectorMath.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Rag/Services/VectorMath.cs) | ✅ Implemented (Step 4.1). Enforces exact `1536`-dimension, finite, positive-norm vectors before persistence or similarity querying. |
| `MarkdownChunker.Chunk(...)` | [MarkdownChunker.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Rag/Services/MarkdownChunker.cs) | ✅ Implemented (Step 4.2). Heading-aware markdown splitter capturing intro prose, skipping empty sections, bounding `1,000` chars / `1,500` UTF-8 bytes (`150` overlap), deterministic `{documentId}#c{index}` IDs, and SHA-256 `ContentHash`. |
| `RagIngestionService.UpsertDocumentAsync(...)` | [RagIngestionService.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Rag/Services/RagIngestionService.cs) | ✅ Implemented (Step 4.3). Idempotent ingestion over `docs/rag-corpus/` and caller notes, skipping unchanged hashes and pruning stale version chunks (`Rag:AutoSeedOnStartup` disabled during `Testing`). |
| `AIProviderSetup.CreateEmbeddingGenerator(...)` | [AIProviderSetup.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Configuration/AIProviderSetup.cs) | ✅ Implemented (Step 4.4). Configures `IEmbeddingGenerator<string, Embedding<float>>` (`OpenRouter`/`OpenAI`/`Ollama`, `1536` dims), OpenRouter privacy headers (`data_collection: "deny"`), and `.UseOpenTelemetry(EnableSensitiveData = false)`. |
| `RagRetrievalService.SearchAsync(...)` | [RagRetrievalService.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Rag/Services/RagRetrievalService.cs) | ✅ Implemented (Step 4.5). Executes PostgreSQL `pgvector` `<=>` distance SQL (`IsNpgsql()`) or C# cosine distance fallback (`!IsNpgsql()`), strictly filtering `OwnerUserId == null \|\| OwnerUserId == authenticatedUserId`. |
| `RagSearchToolFactory` (`search_publishing_guidelines`) | [RagSearchToolFactory.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Rag/Tools/RagSearchToolFactory.cs) | ✅ Implemented (Step 4.5). Host-bound `AIFunction` exposing only `(string query, int topK = 5)` while capturing positive integer `authenticatedUserId` outside the model schema. |
| `CompositeToolProvider` | [CompositeToolProvider.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Mcp/CompositeToolProvider.cs) | ✅ Implemented (Step 4.5). Validates caller `userId > 0` before opening `/mcp`, disposes `/mcp` safely on failure, and merges `/mcp` tools with `RagSearchTool`. |
| `EvidenceLedger` RAG Indexing, Pre-Call Validation & Stage 2 Document Citation Verification | [EvidenceLedger.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Orchestration/EvidenceLedger.cs) & [AgentOutputValidator.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Validation/AgentOutputValidator.cs) | ✅ Implemented (Step 4.6). Pre-call argument validation (`TryValidateToolRequest`), two-pass fail-closed hit indexing (`TryIndexRagSearchResult`), `_ragSearchesExecuted` empty-search protection, and Stage 2 prompt injection defense. |
