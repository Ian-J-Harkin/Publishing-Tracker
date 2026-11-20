# PublishingTracker MCP & Agent — Detailed Design & Decision Log

Companion document to [MCP-Agent-Project-Plan.md](../MCP-Agent-Project-Plan.md).

**Expanded Phase Specifications & Completion Waypoints:**
- [Phase 0 — Baseline, `.NET 10` Upgrade, Auth Hygiene, and Contracts](Phase-0-Baseline-and-Contracts.md) — ✅ **Completed** (41/41 tests passing)
- [Phase 1 — Read-Only Catalogue Capabilities & Hosted `/mcp` Endpoint](Phase-1-Read-Only-Catalogue-and-MCP.md) — ✅ **Completed** (54/54 tests passing)
- [Phase 2 — Agent Loop, Dual Budgets, and Basic Tracing](Phase-2-Agent-Loop-and-Tracing.md) — ✅ **Completed** (20/20 Agent tests, 74/74 total passing)
- [Phase 3 — Linked Evidence Ledger & Reliable Final Outputs](Phase-3-Evidence-Ledger-and-Reliable-Outputs.md) — ✅ **Completed** (22/22 Phase 3 tests, 42/42 Agent tests, 96/96 total passing)
- [Phase 4 — RAG Module with Tenant-Bound pgvector Retrieval](Phase-4-RAG-Module-with-pgvector.md) — ✅ **Completed** (16/16 Phase 4 tests, 58/58 Agent tests, 112/112 total passing)
- Phase 5 — Layered Evaluation Strategy & Continuous Verification — ⚪ *Planned*
- Phase 6 — Deployment, Containerization, & Operational Readiness — ⚪ *Planned*

**Operational & Integration Guides:**
- [MCP User & Integration Guide](MCP-User-Guide.md) — How to authenticate and connect desktop/IDE clients (Claude Desktop, Cursor) or programmatic callers.
- [MCP Testing & Verification Guide](MCP-Testing-Guide.md) — How to run the automated xUnit suite and perform manual JSON-RPC verification with curl/PowerShell.
- [MCP & Agent Programming Guide](MCP-Agent-Programming-Guide.md) — Reference inventory of functions, contracts, endpoints, and file locations added across Phases 0–3.

This log records Architecture Decision Records (ADRs), design Q&As, and concrete technical contracts (tool I/O schemas, metadata completeness rules, identity propagation, output schemas, and evaluation specs) so the master project plan stays concise while implementation details remain explicit and traceable.

---

## Architecture Decision Log

### ADR-001: Hosted `/mcp` Inside `PublishingTracker.Api` (Authoritative Auth & Data Boundary)
- **Status:** Accepted (Updated post-appraisal)
- **Context:** The initial design proposed a separate `PublishingTracker.Mcp` service making outbound HTTP calls back to `PublishingTracker.Api`, introducing a third container and an extra JWT-forwarding hop. Meanwhile, inspection of `PublishingTrackerDbContext.cs` showed global multi-tenant query filters bypass isolation when `_currentUserService.UserId` is `null` and do not cover `Edition` or `PlatformEditionMetadata`.
- **Decision:** Keep MCP tools in an in-repo module (`PublishingTracker.Mcp`) mapped directly inside `PublishingTracker.Api` at `/mcp` (`app.MapMcp("/mcp").RequireAuthorization()`), calling authenticated Application/CQRS handlers that require an explicit non-null `userId`. Keep `PublishingTracker.Rag` as a library/module invoked by the Agent Host rather than a separate service.
- **Consequences:** Eliminates an extra deployable container and one JWT-forwarding hop while preserving a standards-compliant MCP endpoint for both standalone MCP clients and `PublishingTracker.Agent`. Treats EF global filters strictly as defense-in-depth rather than the primary authorization boundary.

### ADR-002: Phase 0 Domain Baseline Reconciliation & `.NET 10 LTS` Upgrade
- **Status:** Accepted (Updated post-appraisal)
- **Context:**
  - `Directory.Build.props` currently targets `net9.0`, and `.NET 9` reached end-of-support on November 10, 2026 (`ai-code-review-bot` already targets `net10.0`).
  - Uncommitted refactoring across `Book.cs`, `Edition.cs`, `PlatformEditionMetadata.cs`, and `PublishingTrackerDbContext.cs` left `DbContext` referencing `Isbn` (casing mismatch) plus properties absent from `Edition.cs` (`Title`, `Subtitle`, `Publisher`, `Language`, `EditionType`, `DeliveryMethod`, `VolumeNumber`, `Audience`, `LitChartId`), and `Book.AddEdition(...)` has an unfinished `narrator`/`duration` block.
- **Decision:** Upgrade `Directory.Build.props`, NuGet packages, and CI workflows to **`.NET 10 LTS` (`net10.0`)** in Phase 0, finish the simplified `Book` + `Edition` + `PlatformEditionMetadata` model in `Book.cs` and `Edition.cs`, and align `PublishingTrackerDbContext.cs`, migrations, DTOs, and handlers so the existing solution compiles and passes tests before adding MCP or Agent components.
- **Consequences:** Runs on a supported LTS runtime matching `ai-code-review-bot` and establishes a clean build/test baseline.

### ADR-003: Phase 1 Initial MCP Tool Scope
- **Status:** Accepted
- **Context:** Three originally proposed tools (`get_price_on_date`, `get_edition_history`, `list_titles_by_status`) depend on effective-dated pricing, immutable revision logs, or title-level lifecycle states that do not exist in the current domain model (`Edition.BasePrice` is current price only; `PlatformEditionMetadata.IsPublished` is per-platform).
- **Decision:** Scope Phase 1 to two read-only tools backed by real domain data:
  1. `find_titles_missing_metadata`
  2. `get_title_details`
  Defer `get_price_on_date`, `get_edition_history`, and `list_titles_by_status` to a separate domain milestone once supporting tables and history semantics exist.
- **Consequences:** Delivers a genuine multi-step agent loop ("find titles with missing metadata, then inspect details for a specific title") without fabricating historical or lifecycle claims.

### ADR-004: PostgreSQL + `pgvector` Standardization (Docker Local/CI & External Neon)
- **Status:** Accepted (Updated post-appraisal)
- **Context:** `Program.cs` currently configures `UseNpgsql` (`NeonConnection`) when running in Azure and `UseSqlServer` (`DefaultConnection`) locally, `TestWebAppFactory.cs` hardcodes SQL Server LocalDB, and `PlatformEditionMetadata.MetadataJson` uses PostgreSQL-specific `.HasColumnType("jsonb")`.
- **Decision:** Standardize on **PostgreSQL** across local development (Docker / local Postgres), integration tests (`TestWebAppFactory.cs`), and production (**Neon**, an external PostgreSQL provider—not an Azure-provisioned resource), and use **`pgvector`** with simple cosine queries for Phase 4 RAG (deferring HNSW/IVFFlat tuning for the small initial corpus).
- **Consequences:** Eliminates local/test/prod RDBMS divergence (explicitly scoped as Phase 0 migration work), aligns with `jsonb`, and keeps relational catalogue records and vector chunks in one database engine.

### ADR-005: Streamable HTTP Only & Explicit JWT Security Model
- **Status:** Accepted (Updated post-appraisal)
- **Context:** Supporting `stdio` alongside HTTP requires storing a static dev bearer token in environment/config, risking static identity reuse, while forwarding raw `Authorization` headers without validating caller authentication and token hygiene leaves trust boundaries unclear. Additionally, `TokenService.cs` currently issues 7-day local-time JWTs (`DateTime.Now.AddDays(7)`) without `JwtRegisteredClaimNames.Iat`, causing `RoleValidationMiddleware.cs` to skip role-change checks.
- **Decision:**
  - Use **Streamable HTTP (`ModelContextProtocol.AspNetCore`) only** for `/mcp` (both standalone MCP client testing and Agent Host calls).
  - Validate caller JWTs explicitly (issuer, audience, lifetime, signing key, and `RoleValidationMiddleware`) at **both** `PublishingTracker.Agent` and `PublishingTracker.Api` (`/mcp`).
  - Add `JwtRegisteredClaimNames.Iat` and use **`DateTime.UtcNow.AddMinutes(expiryMinutes)`** (default **`60` minutes**, configurable via `Jwt:ExpiryMinutes`) in `TokenService.cs` during Phase 0.
  - Attach outbound Bearer tokens **per `HttpRequestMessage`** (never mutating shared `HttpClient.DefaultRequestHeaders`) over TLS.
- **Consequences:** Removes static `stdio` token risk, gives every `/mcp` and Agent request cryptographic caller validation, and makes token revocation/expiry deterministic.

### ADR-006: Agent Orchestration Stack (`Microsoft.Extensions.AI` vs. Semantic Kernel)
- **Status:** Accepted
- **Context (Design Q&A #1):** Inspection of `Directory.Packages.props` and `.csproj` files confirmed `PublishingTracker` currently has **no** AI, LLM, or Semantic Kernel packages—only `MediatR` (CQRS), `FluentValidation`, `EF Core`, and `CsvHelper`.
- **Decision:** Use `Microsoft.Extensions.AI` (`IChatClient`) with a small explicit orchestration loop rather than Semantic Kernel.
- **Rationale:**
  1. **Zero-glue MCP interoperability:** `ModelContextProtocol` exposes `McpClientTool` deriving directly from `Microsoft.Extensions.AI.AIFunction`.
  2. **Explicit loop & guard control:** `IChatClient` represents tool calls without auto-executing them unless a function-invocation wrapper is added, letting host code cleanly enforce `MaxModelTurns`, `MaxTotalToolCalls`, payload limits, normalized repeat-call detection, Evidence Ledger indexing, host-recorded `toolsUsed`, and bounded validation retries.
  3. **Deterministic eval ergonomics:** Writing a scripted fake `IChatClient` (`ScriptedChatClient`) for fast unit evals is lightweight compared to mocking a Semantic Kernel `Kernel` and DI container.
  4. **Ecosystem nuance:** While Microsoft introduced Microsoft Agent Framework for multi-agent workflows, Semantic Kernel remains maintained, builds on top of `Microsoft.Extensions.AI`, and still appears in .NET job specs. Using `Microsoft.Extensions.AI` adopts the shared .NET AI foundation directly.

### ADR-007: Linked Evidence Ledger, Separate Structured Output Turn, & Honest Provenance
- **Status:** Accepted (Updated post-appraisal)
- **Context:**
  - Checking independent sets of `AllowedBookIds` / `AllowedEditionIds` / `AllowedChunkIds` would allow mismatched `(bookId, editionId)` pairs or fabricated field claims.
  - Applying `ChatResponseFormat.ForJsonSchema<T>()` during tool-selection turns is not uniformly supported alongside tool calling across OpenRouter providers/models.
- **Decision:**
  - Populate `toolsUsed` strictly from host execution state and use `"answered"` / `"could_not_answer"` status instead of numeric confidence scores.
  - Replace independent ID sets with a per-run **Evidence Ledger** recording linked `CatalogueEvidenceEntry(int BookId, int? EditionId, IReadOnlySet<string> ReturnedFields)` and `DocumentEvidenceEntry(string DocumentId, string ChunkId, string Excerpt)` items.
  - Run tool-selection turns **without** strict `ResponseFormat` schemas; once tool calls complete, request/validate the final JSON schema and validate linked citations locally in `AgentOutputValidator`.
  - Explicitly note that host validation proves **source and field provenance** against the run's Evidence Ledger, not free-text semantic entailment.
  - Trigger at most 2 repair retries on validation failure (counted against `MaxModelTurns` and overall deadline), then fail clearly.

### ADR-008: Model & Embedding Provider Strategy (OpenRouter + Claude, Multi-Provider Matrix, & External Data Policy)
- **Status:** Accepted (Updated post-appraisal)
- **Context:** The sibling project in `C:\Github\ai-code-review-bot` already proved multi-provider routing (`OpenRouter`, `Ollama`, `OpenAI`, `AzureOpenAI`) and offline routing tests (`RouterTests` with `RecordingHttpMessageHandler`), and its own smoke-test review (`openrouter-smoke-test.md`) recommended evolving the flat config into hardened per-provider `Options` classes with `ValidateOnStart()`. Because OpenRouter tool-calling and strict JSON schema behavior depend on the specific model and routed provider endpoint, configuration must be pinned and verified live.
- **Decision:**
  - Retain the multi-provider configuration matrix (`OpenRouter` with a pinned Claude model/provider config primary; `Ollama`, `OpenAI`, and `AzureOpenAI` fallbacks) using hardened `AIProviderOptions` (`enum`, `init`, `[JsonIgnore]`, redacting `ToString()`, `ValidateOnStart()`, HTTPS checks).
  - **Locked Embedding Model (Phase 4):** Standardize on **`openai/text-embedding-3-small`** (`1536` dimensions, `pgvector` `vector(1536)` column) via OpenRouter / OpenAI so local development and Azure/Neon production share one API key and fixed vector schema without dimension mismatch.
  - **External Provider Data Policy:** Strip all user PII (emails, names, JWTs, credentials) from model payloads—only authorized `Book`/`Edition` metadata and non-sensitive RAG snippets are sent—and enforce OpenRouter `data_collection: "deny"` (no provider retention/training).
- **Consequences:** Reuses proven OpenRouter routing and test patterns, keeps chat and `vector(1536)` embeddings consistent across environments, and enforces clear external-LLM data boundaries.

### ADR-009: Post-Appraisal Security, Budget, and Layered Eval Hardening
- **Status:** Accepted
- **Context:** An external security and architecture appraisal identified three high-priority risks and several medium-priority refinements in the initial sketches.
- **Decision:**
  1. **Non-model-controlled tenant identity:** Never include `userId` or tenant parameters in any `[McpServerTool]` or `AIFunctionFactory.Create` schema (fixing the initial `RagSearchTool` sketch that exposed `callerUserId`). Always bind `authenticatedUserId` from validated `HttpContext.User` / `ICurrentUserService` in host code outside the model-visible signature.
  2. **Dual loop budgets & payload caps:** Enforce both `MaxModelTurns` and `MaxTotalToolCalls` (incremented on every tool execution inside a multi-tool response), plus maximum serialized tool-result size.
  3. **Layered evals:** Keep most of the 15–25 deterministic eval cases as fast in-memory `ScriptedChatClient` + stub-tool unit tests for orchestration/provenance, run a focused PostgreSQL + `/mcp` + Agent integration subset (`TestWebAppFactory`) for two-user isolation and real HTTP/DB plumbing, and keep live OpenRouter/Claude smoke tests opt-in (`[Trait("Category", "LiveEval")]`).

---

## Locked Technical Specifications

> Contracts and rules below are locked for Phases 0–4 based on `Book.cs`, `Edition.cs`, and `PlatformEditionMetadata.cs`.

### 1. Metadata Completeness Rules (`find_titles_missing_metadata`) — Locked

| Scope / Format | Required Fields | Rule Details |
|---|---|---|
| **Book (all titles)** | `Description`, `Genre`, `CoverImageUrl`, >= 1 `Edition`, and conditional `SeriesNumber` | Whitespace-only strings count as missing. `SeriesNumber` (`> 0`) is required **only when `SeriesName` is non-empty**. |
| **Edition (all formats)** | `PublicationDate`, `BasePrice` | `PublicationDate` must be non-null. `BasePrice >= 0` is valid (`0.00m` allowed for free/promo titles); `null` is missing. |
| **Print (`Paperback`, `Hardcover`, `LargeText`)** | `ISBN`, `PageCount` (`> 0`), `Dimensions` (non-empty) | Print formats strictly require an `ISBN`. |
| **Digital (`Ebook`)** | Identifier (`ISBN` **or** >= 1 `PlatformEditionMetadata.PlatformProductId`), `FileSize` (`> 0`) | `Ebook` does not fail for missing `ISBN` if a non-empty `PlatformProductId` (e.g., ASIN/Kobo ID) exists. |
| **Audio (`Audiobook`)** | Identifier (`ISBN` **or** >= 1 `PlatformEditionMetadata.PlatformProductId`), `Narrator` (non-empty), `Duration` (`> TimeSpan.Zero`) | `Audiobook` allows either `ISBN` or `PlatformProductId`. Note: fix `Book.AddEdition` in Phase 0 so `narrator` and `duration` are stored on creation. |

### 2. Pagination & Truncation Policy — Locked

- `find_titles_missing_metadata` returns `page`, `pageSize` (default `20`, max `50`), `totalCount`, and `hasMore`.
- The agent may request **at most 2 pages** per run within `MaxTotalToolCalls`.
- Whenever the last fetched page still has `hasMore == true`, `EvidenceLedger` records truncation and `AgentOutputValidator` requires `limitations` to be non-empty and state that results were paginated/truncated.

### 3. Loop Budget & Timeout Defaults (`AgentLoopOptions`) — Locked

| Setting | Default | Purpose |
|---|---|---|
| `MaxModelTurns` | `6` | Caps total `IChatClient` turns per run (Stage 1 tool turns + Stage 2 final turn) |
| `MaxTotalToolCalls` | `8` | Caps individual tool invocations across all turns |
| `MaxValidationRetries` | `2` | Caps Stage 2 JSON/provenance repair attempts |
| `RepeatCallLimit` | `2` | Halts on repeated identical normalized `(toolName, args)` calls |
| `MaxToolPayloadBytes` | `16_384` (16 KB) | Bounds serialized tool payload stored in prompt history |
| `PerCallTimeout` | `20s` | Bounds individual model or tool calls |
| `OverallTimeout` | `60s` | End-to-end request deadline |

### 4. Initial RAG Corpus & Vector Schema (`docs/rag-corpus/`) — Locked

- **Vector schema:** `pgvector` `vector(1536)` generated with `openai/text-embedding-3-small` via OpenRouter / OpenAI.
- **Initial 4 repo-owned markdown guides** (stored under `Publishing-Tracker/apps/api/docs/rag-corpus/` or `docs/rag-corpus/` with `OwnerUserId = null`, supplemented by user-scoped test notes in isolation tests):
  1. `01-metadata-completeness-guide.md`
  2. `02-print-and-ebook-format-specs.md`
  3. `03-audiobook-production-checklist.md`
  4. `04-platform-publishing-rules.md`

### 5. MCP Tool Contracts (Phase 1, `/mcp` Streamable HTTP)

#### `find_titles_missing_metadata`
- **Inputs (Model-Visible):** `format` (optional `EditionFormat`), `missingField` (optional string), `page` (int, default `1`, min `1`), `pageSize` (int, default `20`, max `50`). *(No `userId` parameter—bound server-side from validated JWT.)*
- **Output envelope:**
  ```json
  {
    "page": 1,
    "pageSize": 20,
    "totalCount": 2,
    "hasMore": false,
    "items": [
      {
        "bookId": 42,
        "title": "Example Title",
        "missingBookFields": ["Description", "CoverImageUrl"],
        "editions": [
          {
            "editionId": 108,
            "format": "Audiobook",
            "missingFields": ["Narrator", "Duration"]
          }
        ]
      }
    ]
  }
  ```

#### `get_title_details`
- **Inputs (Model-Visible):** `bookId` (required int, `> 0`). *(Ownership enforced server-side from validated JWT.)*
- **Output:** Full `Book` metadata + `editions[]` + `platformSummaries[]`.
- **Error mapping:** `404` or non-owned `bookId` returns a single indistinguishable structured error (`NOT_FOUND_OR_UNAUTHORIZED`) so cross-user existence is never leaked.

### 6. Agent Host Response Schema (Phase 3 / Phase 4)

```json
{
  "schemaVersion": "1.0",
  "status": "answered | could_not_answer",
  "answer": "string",
  "evidence": {
    "catalogueSources": [
      {
        "bookId": 42,
        "editionId": 108,
        "fields": ["Narrator", "Duration"],
        "claim": "Audiobook edition 108 is missing Narrator and Duration."
      }
    ],
    "documentSources": [
      {
        "documentId": "doc-metadata-guide-v1",
        "chunkId": "doc-metadata-guide-v1#c3",
        "claim": "Audiobook listings require narrator credit and runtime before submission."
      }
    ]
  },
  "toolsUsed": [
    {
      "toolName": "find_titles_missing_metadata",
      "turn": 1,
      "durationMs": 48,
      "succeeded": true
    }
  ],
  "limitations": "Optional note if results were paginated (hasMore=true) or evidence was partial."
}
```
*(Note: `toolsUsed` is overwritten/populated by the host from execution state, not accepted from the model.)*

---

## High-Level Implementation Sketch (Corrected Post-Appraisal)

> **Note:** Package signatures below (`ModelContextProtocol.AspNetCore`, `Microsoft.Extensions.AI`, `OpenAI`, `Pgvector`) remain illustrative sketches; exact NuGet versions and signatures will be pinned and verified against `.NET 10` during Phases 0–2.

### 1. Folder & Project Layout

```text
Publishing-Tracker/apps/api/
  Directory.Build.props                        # net10.0 LTS
  Directory.Packages.props                     # Pin MCP, M.E.AI, OpenAI, Pgvector, OTel versions
  prompts/
    catalogue-agent-v1.md                      # Versioned prompt + untrusted-data & PII boundary rules
  src/
    PublishingTracker.Domain/                  # Book, Edition, PlatformEditionMetadata
    PublishingTracker.Application/             # CQRS handlers + FormatCompletenessRules.cs
    PublishingTracker.Infrastructure/          # DbContext (Npgsql + pgvector), TokenService (+iat)
    PublishingTracker.Mcp/                     # In-repo MCP module (CatalogueTools.cs)
    PublishingTracker.Rag/                     # RagDocumentChunk, MarkdownChunker, owner-bound RagSearchTool
    PublishingTracker.Api/                     # REST API + MapMcp("/mcp").RequireAuthorization()
    PublishingTracker.Agent/                   # ASP.NET Core Agent Host
      Configuration/AIProviderOptions.cs       # Multi-provider options + ValidateOnStart()
      Configuration/AIProviderSetup.cs         # IChatClient & IEmbeddingGenerator factory
      Configuration/AgentLoopOptions.cs        # MaxModelTurns, MaxTotalToolCalls, MaxToolPayloadBytes
      Auth/PerRequestBearerHandler.cs          # Attaches validated caller JWT per HttpRequestMessage
      Endpoints/AgentEndpoints.cs              # POST /api/agent/query [Authorize]
      Orchestration/AgentLoopRunner.cs         # Explicit loop + two-stage structured output
      Orchestration/EvidenceLedger.cs          # Linked (bookId, editionId?, fields) & (docId, chunkId) ledger
      Validation/AgentOutputValidator.cs       # Schema + linked ledger provenance validation
  tests/
    PublishingTracker.Api.Tests/               # Unit + PostgreSQL integration tests
    PublishingTracker.Evals/                   # Layered evals
      Routing/ProviderSetupTests.cs            # Adapted from ai-code-review-bot/RouterTests
      Fakes/ScriptedChatClient.cs              # Deterministic IChatClient test double
      Unit/OrchestrationEvalTests.cs           # Fast in-memory orchestration/provenance/injection tests
      Integration/AgentMcpIsolationTests.cs    # Focused PostgreSQL + /mcp + two-user isolation suite
      Live/LiveSmokeEvalTests.cs               # [Trait("Category", "LiveEval")] OpenRouter/Claude smoke tests
```

### 2. Multi-Provider Configuration (`AIProviderSetup.cs`) on `.NET 10`

Adapts `C:\Github\ai-code-review-bot` (`AIProviderSetup.cs` + `openrouter-smoke-test.md` improvements) without any undeclared variables:

```csharp
// Sketch: PublishingTracker.Agent/Configuration/AIProviderSetup.cs
public static IChatClient CreateChatClient(
    AIProviderOptions options,
    HttpClient? customHttpClient = null) // Passed by tests (RecordingHttpMessageHandler); null in prod
{
    var chat = ResolveChatSettings(options); // Resolves OpenRouter / Ollama / OpenAI / AzureOpenAI + env fallbacks

    var openAiOptions = new OpenAIClientOptions
    {
        Endpoint = new Uri(chat.Endpoint),
        Transport = customHttpClient is not null ? new HttpClientPipelineTransport(customHttpClient) : null
    };

    var client = new OpenAIClient(new ApiKeyCredential(chat.ApiKey), openAiOptions);

    return client
        .GetChatClient(chat.ModelId) // e.g., pinned "anthropic/claude-sonnet-4" on OpenRouter
        .AsIChatClient()
        .AsBuilder()
        .UseOpenTelemetry(sourceName: "PublishingTracker.Agent", configure: o =>
        {
            // Never log raw prompts, tool arguments, documents, or tokens
            o.EnableSensitiveData = false;
        })
        .Build();
}
```

### 3. Hosted `/mcp` Endpoint in `PublishingTracker.Api` & Per-Request JWT Handling

Hosting `/mcp` directly inside `PublishingTracker.Api` (`ADR-001`, `ADR-005`) validates the caller's JWT on entry and keeps identity inside server-side `ICurrentUserService`:

```csharp
// Sketch: PublishingTracker.Api/Program.cs
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<CatalogueTools>();

// ... after UseAuthentication(), UseAuthorization(), UseRoleValidation()
app.MapMcp("/mcp").RequireAuthorization();
```

```csharp
// Sketch: PublishingTracker.Mcp/Tools/CatalogueTools.cs
[McpServerToolType]
public sealed class CatalogueTools(ISender mediator, ICurrentUserService currentUser)
{
    [McpServerTool, Description("Find books and editions owned by the caller that are missing required metadata.")]
    public async Task<MissingMetadataPageDto> FindTitlesMissingMetadataAsync(
        string? format = null, string? missingField = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Authenticated user required.");
        return await mediator.Send(new FindMissingMetadataQuery(userId, format, missingField, page, pageSize), ct);
    }

    [McpServerTool, Description("Get full book metadata, editions, and platform status summaries for a single bookId.")]
    public async Task<TitleDetailsDto> GetTitleDetailsAsync(int bookId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Authenticated user required.");
        return await mediator.Send(new GetTitleDetailsQuery(userId, bookId), ct);
    }
}
```

On the Agent Host side, outbound calls to `/mcp` attach the validated caller JWT **per `HttpRequestMessage`** (never on shared `HttpClient.DefaultRequestHeaders`):

```csharp
// Sketch: PublishingTracker.Agent/Auth/PerRequestBearerHandler.cs
public sealed class PerRequestBearerHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HTTP context for outbound MCP call.");

        if (context.User.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Caller must be authenticated.");

        var rawJwt = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(rawJwt) || !rawJwt.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Missing Bearer token.");

        request.Headers.TryAddWithoutValidation("Authorization", rawJwt);
        return base.SendAsync(request, ct);
    }
}
```

### 4. Non-Model-Controlled Tenant Binding in `PublishingTracker.Rag`

The RAG tool binds `authenticatedUserId` in host code **outside** the `AIFunction` schema so the LLM never sees or controls a `userId` parameter (`ADR-009`):

```csharp
// Sketch: PublishingTracker.Rag/Tools/RagSearchToolFactory.cs
public AIFunction CreateOwnerScopedSearchTool(int authenticatedUserId)
{
    // Model schema exposes ONLY (query, topK); authenticatedUserId is captured by closure.
    return AIFunctionFactory.Create(
        async (string query, int topK = 5, CancellationToken ct = default) =>
        {
            var clampedK = Math.Clamp(topK, 1, 8);
            var queryVector = await _embeddingGenerator.GenerateVectorAsync(query, cancellationToken: ct);

            var hits = await _dbContext.RagDocumentChunks
                .AsNoTracking()
                .Where(c => c.OwnerUserId == null || c.OwnerUserId == authenticatedUserId)
                .OrderBy(c => c.Embedding.CosineDistance(new Vector(queryVector)))
                .Take(clampedK)
                .Select(c => new RagChunkHit(c.DocumentId, c.ChunkId, c.Title, c.Excerpt))
                .ToListAsync(ct);

            return hits;
        },
        name: "search_publishing_guidelines",
        description: "Search publishing guidelines and the caller's metadata notes. Returns documentId, chunkId, title, and excerpt.");
}
```

### 5. Dual-Budget Agent Loop, Separate Structured Output Turn, & Linked Evidence Ledger

```csharp
// Sketch: PublishingTracker.Agent/Orchestration/AgentLoopRunner.cs
public async Task<AgentResponse> RunAsync(string userQuestion, int authenticatedUserId, CancellationToken ct)
{
    using var activity = Telemetry.ActivitySource.StartActivity("agent.run");
    using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    deadlineCts.CancelAfter(_options.OverallTimeout);

    IList<AIFunction> tools = await _toolProvider.GetToolsForRequestAsync(authenticatedUserId, deadlineCts.Token);
    var toolMap = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);

    var ledger = new EvidenceLedger();
    var messages = new List<ChatMessage>
    {
        new(ChatRole.System, _promptProvider.GetSystemPrompt(version: "v1")),
        new(ChatRole.User, userQuestion)
    };

    // Stage 1: Tool-selection turns WITHOUT strict ResponseFormat (avoids OpenRouter tool+schema conflicts)
    var toolOptions = new ChatOptions { Tools = [.. tools] };
    string? candidateAnswerText = null;

    for (var turn = 1; turn <= _options.MaxModelTurns; turn++)
    {
        var response = await CallModelWithTimeoutAsync(messages, toolOptions, deadlineCts.Token);
        messages.AddMessages(response);

        var functionCalls = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .ToList();

        if (functionCalls.Count == 0)
        {
            candidateAnswerText = response.Text;
            break;
        }

        foreach (var call in functionCalls)
        {
            if (!ledger.TryConsumeToolBudget(_options.MaxTotalToolCalls))
                return AgentResponse.Failure("Total tool-call budget exceeded.", ledger.ToolsUsed);

            if (!toolMap.TryGetValue(call.Name, out var tool))
                return AgentResponse.Failure($"Unknown tool '{call.Name}'.", ledger.ToolsUsed);

            var signature = NormalizeCallSignature(call.Name, call.Arguments);
            if (ledger.RecordAndExceedsRepeatLimit(signature, _options.RepeatCallLimit))
                return AgentResponse.Failure($"Repeated identical call limit reached for '{call.Name}'.", ledger.ToolsUsed);

            var result = await ExecuteToolWithTimeoutAndSizeCapAsync(
                tool, call, _options.MaxToolPayloadBytes, ledger, deadlineCts.Token);

            // Links (bookId, editionId?, returnedFields) and (documentId, chunkId, excerpt)
            ledger.RecordToolOutput(call.Name, result);

            messages.Add(new ChatMessage(ChatRole.Tool,
                [new FunctionResultContent(call.CallId, result.BoundedJsonPayload)]));
        }
    }

    if (candidateAnswerText is null)
        return AgentResponse.Failure("Maximum model turn budget reached.", ledger.ToolsUsed);

    // Stage 2: Structured final formatting & linked Evidence Ledger validation (max 2 repair retries)
    var schemaOptions = new ChatOptions { ResponseFormat = ChatResponseFormat.ForJsonSchema<AgentModelDraft>() };
    messages.Add(new ChatMessage(ChatRole.User,
        "Format your grounded findings as JSON matching the required schema, citing only linked evidence from the tool results above."));

    for (var attempt = 0; attempt <= _options.MaxValidationRetries; attempt++)
    {
        var finalTurn = await CallModelWithTimeoutAsync(messages, schemaOptions, deadlineCts.Token);
        var validation = _validator.Validate(finalTurn.Text, ledger);
        if (validation.IsValid)
            return validation.BuildFinalResponse(ledger.ToolsUsed); // Host-populated toolsUsed

        if (attempt == _options.MaxValidationRetries)
            return AgentResponse.Failure("Schema/provenance validation retries exhausted.", ledger.ToolsUsed, validation.Error);

        messages.AddMessages(finalTurn);
        messages.Add(new ChatMessage(ChatRole.User,
            $"Validation failed: {validation.Error}. Fix the JSON and cite only linked book/edition/field or document/chunk IDs returned in this run."));
    }

    return AgentResponse.Failure("Validation retries exhausted.", ledger.ToolsUsed);
}
```

```csharp
// Sketch: PublishingTracker.Agent/Validation/AgentOutputValidator.cs
public ValidationResult Validate(string? rawJson, EvidenceLedger ledger)
{
    if (!TryDeserialize<AgentModelDraft>(rawJson, out var draft))
        return ValidationResult.Fail("Output is not valid JSON matching AgentModelDraft.");

    if (draft.Status is not ("answered" or "could_not_answer"))
        return ValidationResult.Fail("Status must be 'answered' or 'could_not_answer'.");

    foreach (var cat in draft.Evidence.CatalogueSources)
    {
        // Enforces exact (bookId, editionId?) pairing AND checks every cited field was returned
        if (!ledger.HasCatalogueEvidence(cat.BookId, cat.EditionId, cat.Fields))
            return ValidationResult.Fail(
                $"Uncorroborated catalogue link: BookId={cat.BookId}, EditionId={cat.EditionId}, Fields=[{string.Join(", ", cat.Fields)}].");
    }

    foreach (var doc in draft.Evidence.DocumentSources)
    {
        // Enforces exact (documentId, chunkId) pairing
        if (!ledger.HasDocumentEvidence(doc.DocumentId, doc.ChunkId))
            return ValidationResult.Fail($"Uncorroborated document link: DocumentId={doc.DocumentId}, ChunkId={doc.ChunkId}.");
    }

    if (draft.Status == "answered" &&
        draft.Evidence.CatalogueSources.Count == 0 &&
        draft.Evidence.DocumentSources.Count == 0)
    {
        return ValidationResult.Fail("Status 'answered' requires at least one corroborated evidence entry.");
    }

    return ValidationResult.Success(draft);
}
```

### 6. Layered Evaluation Strategy (`PublishingTracker.Evals`)

1. **Unit Layer (`Unit/OrchestrationEvalTests.cs`) — ~12–18 fast in-memory cases:**
   Uses `ScriptedChatClient` + in-memory stub `AIFunction` tools to test `MaxModelTurns`, `MaxTotalToolCalls` (including single turns emitting >N calls), payload size truncation, normalized repeat-call halting, Stage 2 JSON repair retries, linked Evidence Ledger rejections (swapped `bookId`/`editionId`, unreturned fields, swapped `documentId`/`chunkId`), and prompt-injection payloads.
2. **Integration Layer (`Integration/AgentMcpIsolationTests.cs`) — ~4–6 real DB/HTTP cases:**
   Uses `TestWebAppFactory` (PostgreSQL + `pgvector`) + real `/mcp` Streamable HTTP + `PublishingTracker.Agent` to test two-user isolation (User A vs. User B across both MCP tools and RAG chunks), pagination (`hasMore`), missing-`iat` / expired JWT rejection, and real multi-step tool execution.
3. **Provider & Live Smoke Layer (`Routing/ProviderSetupTests.cs` + `Live/LiveSmokeEvalTests.cs`):**
   `RecordingHttpMessageHandler` tests run in CI with zero spend; `[Trait("Category", "LiveEval")]` tests run opt-in against pinned OpenRouter + Claude credentials to confirm real tool-calling and Stage 2 structured output compatibility.

---

## Resolved Design Questions Summary (Ready for Phase 0)

All open design questions have been resolved and locked into ADRs and specifications above:
1. **Embedding Model:** `openai/text-embedding-3-small` (`1536` dims, `vector(1536)` in `pgvector`) via OpenRouter / OpenAI (`ADR-008`).
2. **Completeness Rules:** Print formats require `ISBN`; `Ebook` and `Audiobook` accept either `ISBN` or `PlatformEditionMetadata.PlatformProductId`; `BasePrice >= 0` is valid (`null` is missing); `SeriesNumber` is required only when `SeriesName` is non-empty.
3. **Pagination Policy:** Surface `hasMore`; allow at most 2 pages within budget; require `limitations` when `hasMore` remains `true`.
4. **JWT & External Data Policy:** `DateTime.UtcNow.AddMinutes(60)` default (`Jwt:ExpiryMinutes`) + `JwtRegisteredClaimNames.Iat` (`ADR-005`); strip user PII and set OpenRouter `data_collection: "deny"` (`ADR-008`).
5. **RAG Corpus & Budgets:** 4 repo-owned markdown guides in `docs/rag-corpus/`; `MaxModelTurns = 6`, `MaxTotalToolCalls = 8`, `MaxValidationRetries = 2`, `RepeatCallLimit = 2`, `MaxToolPayloadBytes = 16_384`, `PerCallTimeout = 20s`, `OverallTimeout = 60s`.
