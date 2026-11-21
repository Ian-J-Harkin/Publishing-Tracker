# PublishingTracker Agent: C# MCP and Agent Project Plan

## Purpose

Build one small, finished system that gives an honest "yes" to the things AI engineering job specs ask for:

- building an AI agent that calls tools across several steps
- building and using MCP tools
- RAG with traceable sources
- structured, validated outputs with failure handling
- evaluation (tests for AI behaviour)
- observability
- deployment to production-style infrastructure

It reuses PublishingTracker's data, so the domain is already familiar and the catalogue / data-quality story carries over directly into interviews.

**Principles**

- **Keep it small and finish it.** A complete, deployed, tested project beats an ambitious half-built one.
- **Read-only tools first.** No tool changes data until everything else works.
- **Every stage is demonstrable on its own**, so you can stop at any point and still have something real.
- **Truthfulness over speculation.** The agent and its tools must never imply historical or lifecycle knowledge (such as past prices, revision logs, or publishing workflow states) that the underlying domain model does not actually store.

---

## Locked-In Architecture & Design Decisions

1. **Host the MCP Server Inside `PublishingTracker.Api` (`/mcp`):**
   Map the read-only MCP endpoint directly inside `PublishingTracker.Api` (`MapMcp("/mcp").RequireAuthorization()`, backed by an in-repo module/project `PublishingTracker.Mcp` or `Features/Mcp`). `PublishingTracker.Api` already owns authentication, EF Core, and catalogue queries—so hosting `/mcp` there eliminates a separate MCP container and an extra JWT-forwarding hop while still exposing a real, standards-compliant MCP endpoint for both standalone MCP clients and `PublishingTracker.Agent`. Keep `PublishingTracker.Rag` as a library/module rather than a separate service.
2. **Streamable HTTP Only (No `stdio` Static Tokens):**
   Use **Streamable HTTP (`ModelContextProtocol.AspNetCore`)** as the sole MCP transport for both local standalone MCP client testing and `PublishingTracker.Agent` deployment. All requests require a validated per-request Bearer JWT (attached per `HttpRequestMessage`, never to shared `HttpClient.DefaultRequestHeaders`), avoiding static `stdio` token risks.
3. **Non-Model-Controlled Tenant Identity:**
   Never expose `callerUserId` (or any user/tenant identifier) as a parameter on any `[McpServerTool]` or `AIFunctionFactory.Create` schema. Derive the user ID strictly from validated `HttpContext.User` / `ICurrentUserService` in server/host code.
4. **Explicit Owner Filtering & Token Hygiene:**
   Treat EF Core global query filters (`PublishingTrackerDbContext.cs`) as defense-in-depth only—they bypass tenant isolation when `_currentUserService.UserId` is `null` and do not cover `Edition` or `PlatformEditionMetadata`. Every `Book`, `Edition`, and `RagDocumentChunk` query must explicitly require a non-null authenticated `userId` and filter through the owning book/document. In Phase 0, add `JwtRegisteredClaimNames.Iat` and shorten token expiry in `TokenService.cs` so `RoleValidationMiddleware` and token expiration work predictably.
5. **Target `.NET 10 LTS` (`net10.0`):**
   Upgrade `Directory.Build.props`, packages, and GitHub Actions workflows from `.NET 9` (out of support since November 10, 2026) to `.NET 10 LTS` in Phase 0, matching `ai-code-review-bot`.
6. **Domain Baseline (Phase 0):**
   Finish the simplified `Book` + `Edition` + `PlatformEditionMetadata` domain model in `Book.cs` and `Edition.cs`, and align `PublishingTrackerDbContext.cs`, migrations, DTOs, and handlers so the existing API compiles and passes tests before adding AI/MCP components.
7. **Initial MCP Tool Scope (Phase 1):**
   Start with **`find_titles_missing_metadata`** and **`get_title_details`** backed by real `Book` and `Edition` fields, server-side completeness filtering, and explicit pagination metadata. Defer `get_price_on_date`, `get_edition_history`, and `list_titles_by_status` to a separate domain milestone once effective-dated history and lifecycle models exist.
8. **PostgreSQL + `pgvector` Standardization (Local Docker & External Neon):**
   Standardize on **PostgreSQL** across local development (Docker / local Postgres), integration tests (`TestWebAppFactory.cs`), and production (**Neon**, an external managed PostgreSQL service—not an Azure-provisioned resource), and use **`pgvector`** with simple cosine queries for Phase 4 RAG (deferring HNSW/IVFFlat tuning for the small initial corpus).
9. **Multi-Provider Matrix & External Data Handling Policy:**
   Retain the multi-provider configuration matrix (`OpenRouter` with Claude as primary; `Ollama`, `OpenAI`, and `AzureOpenAI` as fallbacks via `AIProviderSetup`). Explicitly document what data may be sent to external LLM providers (title/edition metadata and non-sensitive guidelines only; omit user account PII such as emails/tokens; pin a tested OpenRouter model/provider route; configure/document OpenRouter retention/training settings).
10. **Linked Evidence Ledger & Separate Structured Output Step:**
    - The Agent Host populates `toolsUsed` strictly from its own execution record and uses `"answered"` / `"could_not_answer"` status rather than numeric confidence scores.
    - Replace independent ID sets with a per-run **Evidence Ledger** linking `(bookId, editionId?, returnedFields)` and `(documentId, chunkId, excerpt)` so mismatched pairs or unreturned fields are rejected.
    - Keep tool-calling turns separate from `ChatResponseFormat.ForJsonSchema<T>()` (offer tools on tool-selection turns; request/validate strict structured JSON after tool execution completes, and always validate locally).
    - Be explicit that host validation verifies **source/field provenance** against the run's Evidence Ledger, not free-text semantic entailment.
11. **Dual Loop Budgets:**
    Enforce both `MaxModelTurns` (model round-trips) and `MaxTotalToolCalls` (individual tool executions across all turns, since one model turn can emit multiple tool calls), plus tool-result payload size limits and normalized repeat-call detection.
12. **Layered Evals & Early Tracing:**
    Introduce OpenTelemetry spans and deterministic `ScriptedChatClient` unit tests in **Phase 2**. Layer the 15–25 xUnit evals into fast in-memory orchestration/validation tests (`ScriptedChatClient` + stub tools), a focused integration suite (`TestWebAppFactory` + PostgreSQL + `/mcp` + two-user isolation), and opt-in budget-limited live OpenRouter/Claude checks.

---

## Architecture

```
 User / Client (Authenticated HTTP + Bearer JWT)
        │
        ▼
 ┌────────────────────────────────────────────────────────────────┐
 │ PublishingTracker.Agent (ASP.NET Core, Microsoft.Extensions.AI)│
 │  - JWT auth validation (issuer/audience/lifetime)              │
 │  - explicit loop: MaxModelTurns, MaxTotalToolCalls, timeouts   │
 │  - per-request Bearer attachment (never DefaultRequestHeaders) │
 │  - Evidence Ledger + local JSON/provenance validator + retries │
 │  - RAG Retrieval Tool (owner-bound userId from HttpContext)    │
 │  - OpenTelemetry spans (run, model turn, tool call)            │
 └──────────────┬────────────────────────────────┬────────────────┘
                │ Streamable HTTP (/mcp)         │ pgvector query
                │ + validated caller JWT         │ (owner-scoped)
                ▼                                ▼
 ┌──────────────────────────────────┐   ┌─────────────────────────┐
 │ PublishingTracker.Api + /mcp     │   │ PostgreSQL + pgvector   │
 │  - AuthZ & RoleValidation        │   │  - Catalogue tables     │
 │  - [McpServerTool] read-only     │   │  - RagDocumentChunks    │
 │    catalogue tools (userId from  │   └─────────────────────────┘
 │    validated HttpContext/service)│     Local Docker / Test DB
 └──────────────┬───────────────────┘     External Neon (Prod)
                │
                ▼
         PostgreSQL (same DB engine)
```

*External LLM Traffic:* `PublishingTracker.Agent` -> OpenRouter (`https://openrouter.ai/api/v1`, pinned Claude model) or configured fallback (`Ollama` / `OpenAI` / `AzureOpenAI`), sending only authorized catalogue/RAG snippets without user PII.

---

## Current Repository Baseline & Technology Choices

### Repository Findings (Working Tree Inspection)

| Area | Current Evidence | Implication |
|---|---|---|
| **Runtime & CI** | `Directory.Build.props` targets `net9.0`, and CI installs `9.0.x` (.NET 9 reached end-of-support on Nov 10, 2026). | Upgrade to `.NET 10 LTS` (`net10.0`) in Phase 0 across projects, packages, and CI workflows. |
| **Auth & Tokens** | `TokenService.cs` issues 7-day JWTs without `JwtRegisteredClaimNames.Iat`; `RoleValidationMiddleware.cs` skips role-change revocation checks when `iat` is missing. | Add `iat`, tighten token lifetime, and validate JWTs explicitly at both Agent and API (`/mcp`) boundaries. |
| **Existing Tenant Filters** | `PublishingTrackerDbContext.cs` global query filters bypass tenant isolation when `_currentUserService.UserId` is null, and only cover `Book`, `Sale`, and `ImportJob` (not `Edition` or `PlatformEditionMetadata`). | Treat global filters as defense-in-depth; enforce explicit authenticated non-null `userId` checks in every query and tool handler. |
| **Pagination** | Current list endpoint defaults to page 1, size 10. | Completeness filtering and bounded pagination (`page`, `pageSize`, `hasMore`) must run server-side before returning tool results. |
| **Metadata Model** | `Book` holds `Title`, `Author`, `SeriesName`, `SeriesNumber`, `Genre`, `Description`, `CoverImageUrl`. `Edition` holds `Format`, `ISBN`, `PublicationDate`, `BasePrice`, `PageCount`, `Dimensions`, `FileSize`, `Narrator`, `Duration`. | Missing-metadata checks are directly viable once format-specific completeness rules are codified. |
| **Domain / DbContext Drift** | `PublishingTrackerDbContext.cs` maps `Isbn` (casing mismatch) plus properties absent from `Edition.cs` (`Title`, `Subtitle`, `Publisher`, `Language`, `EditionType`, `DeliveryMethod`, `VolumeNumber`, `Audience`, `LitChartId`), and `Book.AddEdition` has an unfinished `narrator`/`duration` block. | Phase 0 must reconcile `DbContext`, migrations, DTOs, and handlers to `Book.cs`/`Edition.cs` and establish a green build/test baseline. |
| **Database Provider Split** | `Program.cs` uses external Neon PostgreSQL (`UseNpgsql`) in Azure, `UseSqlServer` locally, and `TestWebAppFactory.cs` hardcodes SQL Server LocalDB, while `PlatformEditionMetadata.MetadataJson` uses `.HasColumnType("jsonb")`. | Standardize local dev and `TestWebAppFactory.cs` on PostgreSQL (Docker) to match Neon in production and enable `pgvector`—treating this migration as real Phase 0 work. |
| **Price / Edition / Status History** | `Edition.BasePrice` is current price only; `PlatformEditionMetadata.IsPublished` is per-platform; no revision history table exists. | Defer `get_price_on_date`, `get_edition_history`, and `list_titles_by_status` until dedicated domain models exist. |
| **CI & Containers** | Existing `.NET` build/test and Bicep infra workflows exist; no Dockerfiles were present in the inspected API tree. | Add Dockerfiles cleanly for `PublishingTracker.Api` (hosting `/mcp`) and `PublishingTracker.Agent` in Phase 6. |

### Technology Choices

| Component | Choice | Notes |
|---|---|---|
| **Runtime** | `.NET 10 LTS` (`net10.0`) | Supported LTS runtime; matches `ai-code-review-bot`. |
| **MCP SDK** | `ModelContextProtocol.AspNetCore` (+ `ModelContextProtocol` client) | Hosted at `/mcp` inside `PublishingTracker.Api` over Streamable HTTP with JWT auth. Verify and pin NuGet versions in `Directory.Packages.props`. |
| **Agent Framework** | `Microsoft.Extensions.AI` (`IChatClient`) + explicit loop | Zero-glue MCP tool integration (`McpClientTool` derives from `AIFunction`), dual loop/tool-call budgets, Evidence Ledger provenance, and lightweight `ScriptedChatClient` unit evals. |
| **Model Provider** | OpenRouter (pinned Claude chat + `openai/text-embedding-3-small` 1536-dim embeddings) + Ollama / OpenAI / Azure OpenAI fallbacks | Adapted from `ai-code-review-bot` (`AIProviderOptions` + `AIProviderSetup` with `ValidateOnStart()`, `init` properties, redaction, OpenRouter `data_collection: "deny"`, and separate `Chat` / `Embedding` configs). |
| **Retrieval Store** | PostgreSQL + `pgvector` | Same PostgreSQL instance used by the API (Docker locally/CI, external Neon in production). Defer HNSW/IVFFlat index tuning until corpus size warrants it. |
| **Primary Interface** | Authenticated ASP.NET Core API | Verify end-to-end via API requests and tests first; any chat UI is optional after Phase 6. |

#### Why `Microsoft.Extensions.AI` (`IChatClient`) over Semantic Kernel

- **No existing AI stack in repo:** `PublishingTracker` currently contains no AI, LLM, or Semantic Kernel packages—only `MediatR` for CQRS—so starting with `Microsoft.Extensions.AI` introduces no migration friction.
- **Zero-glue MCP integration:** `ModelContextProtocol` exposes discovered MCP tools (`McpClientTool`) directly as `Microsoft.Extensions.AI.AIFunction` instances, ready to pass to `IChatClient` without writing `KernelPlugin` / `KernelFunction` wrappers.
- **Explicit loop & guard control:** `IChatClient` represents tool calls without auto-executing them unless a function-invocation wrapper is added. Writing an explicit loop around `IChatClient.GetResponseAsync(...)` keeps `MaxModelTurns`, `MaxTotalToolCalls`, payload caps, Evidence Ledger indexing, host-recorded `toolsUsed`, and bounded validation retries transparent and enforceable.
- **Lightweight deterministic evals:** Unit-layer evals only need a single `ScriptedChatClient : IChatClient` test double rather than mocking a Semantic Kernel `Kernel` and DI container.
- **Nuance on Semantic Kernel & Microsoft Agent Framework:** While Microsoft introduced Microsoft Agent Framework for multi-agent workflows, Semantic Kernel remains maintained, builds on top of `Microsoft.Extensions.AI`, and still appears in .NET job specs. Using `Microsoft.Extensions.AI` adopts the shared .NET AI foundation directly.

---

## Components

### 1. MCP Server Endpoint (Hosted in `PublishingTracker.Api` at `/mcp`)

Exposes authenticated, read-only catalogue tools over Streamable HTTP (`MapMcp("/mcp").RequireAuthorization()`), organized cleanly in an in-repo module (`PublishingTracker.Mcp`) referenced by `PublishingTracker.Api`.

**Initial Release Tools (Phase 1)**

| Tool | What it does |
|---|---|
| `find_titles_missing_metadata` | Returns `bookId`, `editionId` (where applicable), title, format, and specific missing fields for the authenticated caller (`ICurrentUserService.UserId`), using format-specific rules and server-side bounded pagination (`page`, `pageSize`, `hasMore`, `totalCount`). |
| `get_title_details` | Returns a title's `Book` metadata, available `Edition` metadata, and platform publication summary by stable `bookId`, strictly scoped to the authenticated caller. |

**Deferred Candidate Tools (Future Domain Milestone)**

| Tool | Why Deferred |
|---|---|
| `list_titles_by_status` | Requires an explicit title-level lifecycle status model beyond per-platform `IsPublished`. |
| `get_price_on_date` | Requires an effective-dated price-history model (currency, territory, platform, overlap rules). |
| `get_edition_history` | Requires defining immutable revision events vs. listing current editions. |

**Tool Guidelines**

- Never accept `userId` or tenant parameters from tool inputs; derive caller identity strictly from `HttpContext.User` / `ICurrentUserService`.
- Keep each tool small, with typed inputs, explicit validation, cancellation propagation, and clear descriptions.
- Return structured JSON with stable IDs and explicit pagination/truncation indicators (`hasMore`), never prose.
- Map validation, not-found/unauthorized, and upstream errors into consistent tool errors without leaking cross-user existence.

### 2. Agent Host (`PublishingTracker.Agent`, ASP.NET Core)

Runs an explicit, bounded agent loop against `IChatClient` (`OpenRouter` + pinned Claude model primary, with `Ollama`/`OpenAI`/`AzureOpenAI` configuration support).

- **Authentication:** Validates incoming user JWTs (matching issuer/audience/lifetime + role checks) and attaches the caller's Bearer token per outbound `HttpRequestMessage` to `PublishingTracker.Api`'s `/mcp` endpoint (never mutating shared `HttpClient.DefaultRequestHeaders`).
- **Two-Stage Turn Discipline:** Offer tools on tool-selection turns without mixing `ResponseFormat.ForJsonSchema<T>()`. Once tool execution completes, request/validate the final structured JSON response (always validating locally in host code).
- **Dual Loop & Size Controls:** Enforce `MaxModelTurns`, `MaxTotalToolCalls` (counting every individual tool invocation), per-call timeout, overall request deadline, `CancellationToken` propagation, tool-result payload size limits, and normalized repeated-call detection.
- **Per-Run Evidence Ledger:** Index linked `(bookId, editionId?, returnedFields)` and `(documentId, chunkId, excerpt)` entries from actual tool outputs during the run, alongside host-recorded `toolsUsed`.

### 3. RAG Module (`PublishingTracker.Rag` + `pgvector`)

Use RAG where document retrieval adds distinct value over structured catalogue queries:

- Publishing guidelines and metadata/style standards
- Personal/team notes on formats, ISBN/asset rules, and distribution platforms
- Optional book descriptions/blurbs indexed with explicit `OwnerUserId` scope

**Design**

- Keep `PublishingTracker.Rag` as a library/module invoked by the Agent Host—not a separate deployable service.
- Define stable `DocumentId`, `Version`, `ChunkId`, chunk boundaries, and `OwnerUserId` (`null` for shared system guidelines, `int` for user-owned docs).
- Expose retrieval **as a host-bound `AIFunction`** whose schema exposes only `(query, topK)`—binding `authenticatedUserId` strictly from `HttpContext` outside the model-visible signature.
- Use simple `pgvector` cosine distance queries first (defer HNSW/IVFFlat tuning for the small corpus).
- Treat retrieved chunks strictly as untrusted data; test explicitly against embedded prompt-injection instructions.

### 4. Reliable Outputs & Provenance Validation

Final response JSON contract returned by `PublishingTracker.Agent`:

- `status`: `"answered"` or `"could_not_answer"` (no uncalibrated numeric confidence score).
- `answer`: concise grounded response (or explanation when `could_not_answer`).
- `evidence`: linked citations distinguishing **catalogue evidence** (`bookId`, optional `editionId`, `fields`, `claim`) from **document evidence** (`documentId`, `chunkId`, `claim`).
- `toolsUsed`: **populated strictly by the host** from actual execution records, never copied from model output.
- `limitations`: optional explanation of missing data, pagination truncation, or unverified claims.

**Validation & Retry Rules**

- Validate both JSON schema and **linked Evidence Ledger provenance** locally:
  - Every `catalogueSources` entry must match a recorded `(bookId, editionId?)` pair and cite only `fields` returned in that tool output.
  - Every `documentSources` entry must match a recorded `(documentId, chunkId)` pair returned in the current run.
  - Verify status consistency (`answered` vs. `could_not_answer`), while explicitly noting in documentation that structural provenance proves source/field linkage—not free-text semantic entailment.
- On validation failure, retry at most **twice** with a concise validation error (counting retries against `MaxModelTurns`/overall deadline); fail clearly if validation still fails.

### 5. Evaluation (`PublishingTracker.Evals` in `xUnit`)

Layer the 15–25 xUnit cases across three explicit test tiers:

1. **Provider & Routing Unit Tests:** Adapted from `ai-code-review-bot/RouterTests` (`RecordingHttpMessageHandler`) to verify OpenRouter/Ollama/OpenAI/Azure configuration, startup validation, HTTPS checks, and Bearer headers.
2. **Deterministic Orchestration & Provenance Evals (Majority of the 15–25 CI Cases):** Fast in-memory `ScriptedChatClient` + stub tools testing multi-step loops, `MaxModelTurns`, `MaxTotalToolCalls`, payload caps, repeat-call guards, `ResponseFormat`/JSON repair retries, linked Evidence Ledger rejections (mismatched `bookId`/`editionId` or `documentId`/`chunkId`), and prompt-injection text handling. *Explicitly labeled as testing host orchestration/guardrails, not real-model reasoning.*
3. **Focused Integration & Two-User Isolation Tests (`TestWebAppFactory` + PostgreSQL):** Real HTTP calls across Agent -> `/mcp` -> `PublishingTracker.Api` -> PostgreSQL + `pgvector` proving JWT enforcement, owner scoping, pagination, and cross-user isolation.
4. **Opt-In Live Model Smoke Evals (`[Trait("Category", "LiveEval")]`):** Budget-limited checks against a pinned OpenRouter Claude model/provider route verifying live tool-calling and structured output compatibility.

### 6. Observability (`OpenTelemetry`)

- Emit OpenTelemetry traces with a root span per agent run and child spans per model turn and tool call (`EnableSensitiveData = false`).
- Export to Application Insights (or an OTLP backend locally).
- Record: prompt version, model/deployment identifier, OpenRouter/provider route, token counts (when returned), tool name, duration, retry count, and outcome.
- **Privacy, Redaction & External Data Policy:** Do not log raw prompts, tool arguments, retrieved document text, JWTs, or secrets by default; strip user account PII from outbound LLM payloads and document OpenRouter data retention/training policy.

### 7. Deployment and Configuration

- Containerize `PublishingTracker.Api` (hosting `/mcp`) and `PublishingTracker.Agent` with `/health` checks (2 services instead of 3).
- Connect both to external **Neon PostgreSQL (`pgvector`)** in production.
- Extend existing Bicep and `.github/workflows` for `.NET 10` build, layered CI evals, container image build/push, and controlled Azure deployment.
- Keep model names, provider settings, prompt files, step/tool-call limits, and timeouts in versioned configuration with `ValidateOnStart()`; keep API keys, JWT signing keys, and Neon connection strings in secret stores / managed identities.

---

## Phased Implementation Plan

### Waypoint & Phase Completion Tracker

| Phase | Title | Status | Completion Waypoint / Artifacts | Verified Tests |
|---|---|:---:|---|:---:|
| **Phase 0** | Baseline, `.NET 10`, Auth & Contracts | ✅ **Completed** | [Phase-0-Baseline-and-Contracts.md](docs/Phase-0-Baseline-and-Contracts.md)<br>Reconciled entities, Postgres migration, `TokenService` `iat` + 60m expiry | 41/41 |
| **Phase 1** | Read-Only Catalogue & Hosted `/mcp` | ✅ **Completed** | [Phase-1-Read-Only-Catalogue-and-MCP.md](docs/Phase-1-Read-Only-Catalogue-and-MCP.md)<br>`PublishingTracker.Mcp`, `find_titles_missing_metadata`, `get_title_details`, `/mcp` streamable HTTP, 2-user isolation | 54/54 |
| **Phase 2** | Agent Loop, Dual Budgets & Tracing | ✅ **Completed** | [Phase-2-Agent-Loop-and-Tracing.md](docs/Phase-2-Agent-Loop-and-Tracing.md)<br>`PublishingTracker.Agent`, OpenRouter + Claude, dual loop budgets (6 turns, 8 tool calls), `ActivitySource` tracing | 20/20<br>(74/74 total) |
| **Phase 3** | Evidence Ledger & Reliable Outputs | ✅ **Completed** | [Phase-3-Evidence-Ledger-and-Reliable-Outputs.md](docs/Phase-3-Evidence-Ledger-and-Reliable-Outputs.md)<br>Per-run `EvidenceLedger` tracking linked source fields, Stage 2 structured repair turns | 22/22<br>(96/96 total) |
| **Phase 4** | RAG Module with `pgvector` | ✅ **Completed** | [Phase-4-RAG-Module-with-pgvector.md](docs/Phase-4-RAG-Module-with-pgvector.md)<br>`PublishingTracker.Rag`, `search_publishing_guidelines`, host-bound user scoping | 16/16<br>(112/112 total) |
| **Phase 5** | Layered CI Evaluation Suites | ✅ **Completed** | [Phase-5-Layered-Evaluation-Strategy.md](docs/Phase-5-Layered-Evaluation-Strategy.md)<br>30 Layer 1 deterministic + 4 real `pgvector` Layer 2 + 5 Layer 3 live smoke/baseline | 39/39<br>(151/151 total) |
| **Phase 6** | Deployment & Operational Hardening | ✅ **Completed** | [Phase-6-Deployment-and-Operational-Readiness.md](docs/Phase-6-Deployment-and-Operational-Readiness.md)<br>Chiseled Dockerfiles, single-file `HealthProbe`, `AllowInsecureInternalHttp` guard, OTLP export, Compose & CI updates | 19/19<br>(170/170 total) |

---

### Phase 0 — Baseline, `.NET 10` Upgrade, Auth Hygiene, and Contracts

> Detailed specification: [docs/Phase-0-Baseline-and-Contracts.md](docs/Phase-0-Baseline-and-Contracts.md)

**Goal:** Establish a clean `.NET 10` PostgreSQL build/test baseline, fix token/ownership hygiene, and lock contracts before adding AI/MCP components.

**Work**
1. Upgrade `Directory.Build.props`, NuGet packages (`Directory.Packages.props`), and GitHub Actions CI workflows to `.NET 10 LTS` (`net10.0`).
2. Reconcile `Book.cs`, `Edition.cs`, `PlatformEditionMetadata.cs`, `PublishingTrackerDbContext.cs`, migrations, DTOs, and handlers so the existing solution builds cleanly.
3. Standardize local dev (`Program.cs`) and `TestWebAppFactory.cs` on PostgreSQL (Docker locally, external Neon in production); run and document the baseline test suite.
4. Tighten auth hygiene: add `JwtRegisteredClaimNames.Iat` and switch to **`DateTime.UtcNow.AddMinutes(expiryMinutes)`** (default `60` minutes via `Jwt:ExpiryMinutes`) in `TokenService.cs` so `RoleValidationMiddleware` enforces role changes consistently; document explicit owner-filtering requirements (not relying solely on `DbContext` global query filters).
5. Codify locked format-specific metadata completeness rules (Print formats require `ISBN`, `Ebook`/`Audiobook` accept `ISBN` or `PlatformProductId`, `BasePrice >= 0` valid, `SeriesNumber` required only when `SeriesName` is set), 2-page pagination cap + `limitations` rules, and external LLM policy (strip PII, enforce OpenRouter `data_collection: "deny"`).
6. Define a small, non-sensitive seed catalogue spanning at least two users and owned test guidelines.

**Acceptance**
- Existing API builds on `.NET 10` and passes tests against PostgreSQL.
- `TokenService` emits `iat` with reduced token lifetime.
- Two initial tool contracts, completeness rules, and external LLM data policy are documented.

### Phase 1 — Read-Only Catalogue Capabilities & Hosted `/mcp` Endpoint

> Detailed specification: [docs/Phase-1-Read-Only-Catalogue-and-MCP.md](docs/Phase-1-Read-Only-Catalogue-and-MCP.md)  
> Operational Guides: [MCP User Guide](docs/MCP-User-Guide.md) \| [MCP Testing Guide](docs/MCP-Testing-Guide.md)

**Goal:** Expose two owner-scoped read-only MCP tools from `PublishingTracker.Api` over authenticated Streamable HTTP.

**Work**
1. Add authenticated, read-only queries in `PublishingTracker.Application` / `PublishingTracker.Api` supporting `find_titles_missing_metadata` (server-side filtering + bounded pagination with `hasMore`) and `get_title_details`, explicitly requiring non-null authenticated `userId`.
2. Add `PublishingTracker.Mcp` module (`ModelContextProtocol.AspNetCore`) and map `app.MapMcp("/mcp").RequireAuthorization()` in `PublishingTracker.Api`.
3. Implement `CatalogueTools` (`[McpServerToolType]`) with typed inputs, cancellation propagation, no model-supplied `userId`, and structured error mapping.
4. Add unit and `TestWebAppFactory` + PostgreSQL integration tests covering standalone MCP client discovery/invocation, pagination, invalid inputs, unauthenticated/invalid tokens, and **two-user isolation**.

**Acceptance**
- A standalone MCP client authenticates with a Bearer JWT over Streamable HTTP and invokes both tools at `/mcp`.
- Results contain stable IDs, structured fields, and explicit pagination metadata.
- Two-user isolation tests prove User A cannot read User B's books or editions.
- No tool mutates data or accepts a tenant/user ID from tool arguments.

### Phase 2 — Agent Loop, Dual Budgets, and Basic Tracing

> Detailed specification: [docs/Phase-2-Agent-Loop-and-Tracing.md](docs/Phase-2-Agent-Loop-and-Tracing.md)

**Goal:** Demonstrate a real, bounded multi-step agent with early OpenTelemetry tracing and deterministic tests.

**Work**
1. Create `PublishingTracker.Agent` (ASP.NET Core) with an authenticated endpoint (`POST /api/agent/query`) validating caller JWTs (issuer/audience/lifetime).
2. Port and harden `AIProviderOptions` / `AIProviderSetup` from `ai-code-review-bot` (`IOptions<T>`, `ValidateOnStart()`, redaction, `OpenRouter`/Claude primary + `Ollama`/`OpenAI`/`AzureOpenAI` fallbacks) wiring `IChatClient`.
3. Connect the MCP client to `PublishingTracker.Api`'s `/mcp` endpoint, attaching the caller's validated JWT per outbound `HttpRequestMessage` (never `DefaultRequestHeaders`).
4. Implement explicit `AgentLoopRunner`: tool-selection turns (without mixing `ResponseFormat` schemas) -> validate tool name/args -> execute tool -> append result -> repeat or finish.
5. Enforce locked `AgentLoopOptions` defaults (`MaxModelTurns = 6`, `MaxTotalToolCalls = 8`, `MaxValidationRetries = 2`, `RepeatCallLimit = 2`, `MaxToolPayloadBytes = 16_384`, `PerCallTimeout = 20s`, `OverallTimeout = 60s`), and enforce the max-2-page pagination cap with mandatory `limitations` when `hasMore` remains `true`.
6. Add OpenTelemetry spans (`EnableSensitiveData = false`) for agent run, model turns, and tool calls.
7. Add `ProviderSetupTests` (`RecordingHttpMessageHandler`) and deterministic `ScriptedChatClient` xUnit tests for two-tool sequences and budget/timeout/repeat-call failures.

**Acceptance**
- One authenticated request invokes both MCP tools in sequence and returns a grounded response.
- Turn limit, tool-call limit, payload cap, timeout, unknown tool, and repeat-call scenarios fail explicitly.
- Local OpenTelemetry trace shows the run span and child model/tool spans without leaking tokens or prompts.

### Phase 3 — Linked Evidence Ledger & Reliable Final Outputs

> Detailed specification: [docs/Phase-3-Evidence-Ledger-and-Reliable-Outputs.md](docs/Phase-3-Evidence-Ledger-and-Reliable-Outputs.md)

**Goal:** Ensure every response satisfies the versioned JSON schema and passes linked provenance verification.

**Work**
1. Implement per-run `EvidenceLedger` tracking linked `(bookId, editionId?, returnedFields)` entries from tool results.
2. Add a final structured-output turn/extraction step after tool execution completes, followed by local `AgentOutputValidator` checks.
3. Populate `toolsUsed` exclusively from host execution records.
4. Reject citations whose `(bookId, editionId)` pair was not linked in the run's Evidence Ledger or that reference unreturned fields.
5. Allow at most two validation repair retries (counted against `MaxModelTurns` and overall deadline); fail clearly when exhausted.

**Acceptance**
- Both `"answered"` and `"could_not_answer"` responses pass schema validation.
- Mismatched `(bookId, editionId)` pairs or unreturned field citations are rejected in deterministic tests.
- Retry and exhausted-retry failures are tested and observable in traces.

### Phase 4 — RAG Module with Tenant-Bound `pgvector` Retrieval

> Detailed specification: [docs/Phase-4-RAG-Module-with-pgvector.md](docs/Phase-4-RAG-Module-with-pgvector.md)

**Goal:** Answer questions over publishing guidelines and user notes via `pgvector`, enforcing owner scoping outside the model's control.

**Work**
1. Author the 4 repo-owned markdown guides under `docs/rag-corpus/` (`01-metadata-completeness-guide.md`, `02-print-and-ebook-format-specs.md`, `03-audiobook-production-checklist.md`, `04-platform-publishing-rules.md`) plus user-scoped test notes, and configure `RagDocumentChunk` with `pgvector` `vector(1536)` (simple cosine distance query; defer index tuning).
2. Build deterministic chunking + ingestion pipeline using `openai/text-embedding-3-small` (`1536` dims) via `IEmbeddingGenerator<string, Embedding<float>>`.
3. Expose bounded `RagSearchTool` (`AIFunctionFactory.Create`) taking only `(string query, int topK = 5)`—capturing validated `authenticatedUserId` in host scope so the model cannot pass or alter `userId`.
4. Extend `EvidenceLedger` and `AgentOutputValidator` to record and verify linked `(documentId, chunkId)` pairs.
5. Treat retrieved chunks as untrusted data and add prompt-injection + cross-user RAG isolation tests.

**Acceptance**
- Known questions retrieve relevant chunks and verify linked `(documentId, chunkId)` citations.
- Model schemas expose no `userId` parameter.
- Tests cover `could_not_answer` on missing evidence, stale/deleted chunks, cross-user RAG isolation, and embedded prompt-injection resistance.

### Phase 5 — Layered Evaluation Suite and CI

**Goal:** Run fast, reliable behavioural gates in CI alongside focused integration and opt-in live checks.

**Work**
1. Complete the 15–25 xUnit eval cases in `PublishingTracker.Evals` split cleanly across:
   - Fast in-memory `ScriptedChatClient` + stub-tool cases (loop budgets, repeat-call guards, JSON retries, linked Evidence Ledger rejections, prompt-injection text handling).
   - Focused PostgreSQL + `/mcp` + Agent integration cases (multi-tool end-to-end flow, pagination, unauthorized access, two-user isolation).
2. Wire unit and integration suites into GitHub Actions CI.
3. Add an opt-in, budget-limited `[Trait("Category", "LiveEval")]` runner against a pinned OpenRouter Claude model/provider route and sanitized fixtures, reporting results separately from deterministic CI tests.

**Acceptance**
- 15+ deterministic eval cases run cleanly in CI without requiring live LLM spend.
- Integration suite verifies real `/mcp`, PostgreSQL, and two-user isolation.
- Live-model smoke results document pinned model/provider route, prompt version, and pass/fail counts separately.

### Phase 6 — Deployment, Observability, and Handover

**Goal:** Deploy the two-service topology cleanly to Azure (connected to external Neon PostgreSQL) with full telemetry and redaction.

**Work**
1. Add Dockerfiles and `/health` checks for `PublishingTracker.Api` (hosting `/mcp`) and `PublishingTracker.Agent`.
2. Configure Azure deployment topology (Agent Host + API/MCP + external Neon PostgreSQL with `pgvector`), restricting `/mcp` access and validating JWTs at every boundary.
3. Update Bicep and `.github/workflows` for `.NET 10` CI, container publish, and controlled deployment using least-privilege identities and secret stores.
4. Export redacted OpenTelemetry traces (`EnableSensitiveData = false`) to Application Insights and document retention/external-LLM data policies.
5. Run post-deployment smoke tests (authenticated request, two-tool run, RAG citation check, Application Insights trace inspection) and write clean-checkout README/architecture/cost/teardown docs.

**Acceptance**
- Controlled pipeline deploys both containers cleanly.
- Post-deployment smoke test passes and its redacted trace is visible in Application Insights.
- README instructions, external Neon/OpenRouter configuration, cost notes, and teardown steps are verified.

### Deferred Milestone — Effective-Dated Pricing and Revision History

Keep out of the first release until dedicated domain modeling is prioritized:

1. Define price scope (`Edition`, currency, territory, platform) and effective-date boundary/overlap/correction rules.
2. Add price-history and/or immutable edition-revision tables, migrations, authorized API queries, and historical seed data.
3. Only then add `get_price_on_date`, `get_edition_history`, and `list_titles_by_status` to `/mcp`.

---

## Suggested Repository Layout

Single canonical plan at `MCP-Agent-Project-Plan.md` (with detailed design/ADRs in `docs/MCP-Agent-Design-Log.md`) under `Publishing-Tracker/apps/api`:

```text
Publishing-Tracker/apps/api/
  Directory.Build.props                 # net10.0 LTS
  Directory.Packages.props              # central pinned NuGet versions
  src/
    PublishingTracker.Domain/           # Book, Edition, PlatformEditionMetadata
    PublishingTracker.Application/      # CQRS queries & FormatCompletenessRules
    PublishingTracker.Infrastructure/   # DbContext (Npgsql), TokenService (iat), pgvector
    PublishingTracker.Mcp/              # MCP tools library mapped into Api at /mcp
    PublishingTracker.Rag/              # RagDocumentChunk, chunker, owner-bound RagSearchTool
    PublishingTracker.Api/              # REST API + Streamable HTTP /mcp endpoint
    PublishingTracker.Agent/            # ASP.NET Core agent host, IChatClient loop, EvidenceLedger
  tests/
    PublishingTracker.Api.Tests/        # Existing unit + PostgreSQL integration tests
    PublishingTracker.Evals/            # Provider routing tests + 15–25 layered evals + live smoke
  prompts/                              # Versioned system & structured-output prompts
docs/                                   # MCP-Agent-Design-Log.md, rag-corpus/ (4 markdown guides), trace exports
.github/workflows/                      # .NET 10 CI, layered evals, infra & Azure deploy
```

---

## Build Order & Honest Milestones

| Step | Phase | Deliverable | Status | What You Can Honestly Say Afterwards |
|---|---|---|---|---|
| 0 | Phase 0 | `.NET 10` PostgreSQL baseline, `iat`/auth hygiene, contracts | ✅ Completed | "Upgraded to .NET 10 LTS, reconciled domain drift, and standardized on PostgreSQL." |
| 1 | Phase 1 | Hosted `/mcp` endpoint in API with 2 owner-scoped tools + isolation tests | ✅ Completed | "I've built read-only, user-scoped MCP tools in C# over Streamable HTTP." |
| 2 | Phase 2 | Explicit `IChatClient` loop with dual budgets, tracing, and scripted tests | ✅ Completed | "I've built a bounded multi-step tool-calling agent in .NET with OpenTelemetry tracing." |
| 3 | Phase 3 | Linked Evidence Ledger, separate structured output turn, provenance checks, retries | ✅ Completed | "It validates structured outputs against a per-run Evidence Ledger and handles failures." |
| 4 | Phase 4 | `pgvector` RAG module with host-bound tenant ID and prompt-injection tests | ✅ Completed | "It retrieves from `pgvector` with non-model-controlled tenant isolation and chunk citations." |
| 5 | Phase 5 | 15–25 layered xUnit evals in CI + opt-in live OpenRouter/Claude checks | ✅ Completed | "It has layered deterministic CI evals plus live model smoke verification." |
| 6 | Phase 6 | Containerized Azure + Neon deployment, redacted App Insights traces & docs | ✅ Completed | "I've shipped it to Azure with CI/CD and redacted OpenTelemetry observability." |

---

## Definition of Done

- [x] Solution and CI run on `.NET 10 LTS` (`net10.0`) with tests passing against PostgreSQL
- [x] JWT auth (`iat` claim, explicit validation, non-null `userId` filtering) is enforced at every service boundary and tool query (verified by two-user isolation tests)
- [x] MCP endpoint (`/mcp` in `PublishingTracker.Api`) serves `find_titles_missing_metadata` and `get_title_details` over Streamable HTTP to standalone MCP clients and the Agent Host
- [x] Pagination and incomplete results (`hasMore`) are explicitly surfaced rather than silently truncated
- [x] Agent completes multi-step tasks with explicit `MaxModelTurns`, `MaxTotalToolCalls`, payload limits, timeouts, and repeat-call guards
- [x] Final answers validate locally against a versioned schema with max 2 retries, host-populated `toolsUsed`, and clear failure states
- [x] Linked citations (`(bookId, editionId?, fields)` and `(documentId, chunkId)`) are verified against the run's Evidence Ledger
- [x] RAG retrieval binds caller identity in host code (never model arguments) and withstands prompt-injection test fixtures
- [x] At least 15 deterministic eval cases run in CI (unit orchestration + focused PostgreSQL integration), with live OpenRouter/Claude evals reported separately
- [x] OpenTelemetry traces are visible for a full agent run without exposing raw prompts, documents, tokens, or secrets
- [x] External LLM data handling/retention policy is documented and PII is stripped from model payloads
- [x] No price, status, or revision-history claim is made without supporting domain data
- [x] Deployed to Azure (with external Neon PostgreSQL) via GitHub Actions with documented architecture, sample trace, cost, and teardown notes

---

## Risks and Cautions

- **Library churn:** `ModelContextProtocol.AspNetCore`, `Microsoft.Extensions.AI`, and `OpenAI` adapters evolve quickly. Pin verified `.NET 10`-compatible versions in `Directory.Packages.props` before coding each phase.
- **Provider-specific OpenRouter quirks:** Tool calling and strict JSON schema support vary by routed model/provider on OpenRouter; pin a tested Claude model/provider config and confirm both with live smoke tests.
- **External data exposure:** Never send raw user PII (emails, JWTs) or unapproved sensitive data to external LLM endpoints; document OpenRouter data retention settings.
- **Scope creep:** Resist adding effective-dated pricing, edition history, write tools, HNSW/IVFFlat tuning, or a custom UI before Phases 0–6 pass end-to-end.
- **Auth & provenance bypass:** Never expose `userId` in model-visible schemas, never rely solely on nullable EF global filters, never share mutable `DefaultRequestHeaders`, and never trust model-reported `toolsUsed`.

---

## Interview Points This Project Supports

- **Security & service boundaries:** Why `/mcp` lives inside `PublishingTracker.Api`, why tenant IDs never appear in tool schemas, how JWT validation and per-request Bearer attachment work, and why EF global filters are treated only as defense-in-depth.
- **Reliability & provenance:** Dual `MaxModelTurns` + `MaxTotalToolCalls` budgets, separating tool-selection turns from structured-output turns, host-recorded `toolsUsed`, and validating linked `(bookId, editionId, fields)` / `(documentId, chunkId)` records via an Evidence Ledger.
- **Framework vs. explicit orchestration:** Why `Microsoft.Extensions.AI` (`IChatClient`) with an explicit loop was chosen over Semantic Kernel auto-invocation for transparent budgets, repeat-call guards, MCP `AIFunction` interoperability, and deterministic testing.
- **RAG & verification:** Why relational SQL and `pgvector` serve distinct jobs, defending against prompt injection in retrieved text, and separating deterministic CI orchestration evals from live OpenRouter/Claude smoke tests.
