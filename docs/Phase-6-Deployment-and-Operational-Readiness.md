# Phase 6 — Expanded Design: Deployment, Containerization, & Operational Readiness

> [!NOTE]
> **Status: Specification & Design Only — Unimplemented.**
> Code implementation has not begun. The codebase is currently at Phase 4 (112/112 tests passing across Phases 0–4).

Companion phase specification to [MCP-Agent-Project-Plan.md](../MCP-Agent-Project-Plan.md), [MCP-Agent-Design-Log.md](MCP-Agent-Design-Log.md), [Phase-0-Baseline-and-Contracts.md](Phase-0-Baseline-and-Contracts.md), [Phase-1-Read-Only-Catalogue-and-MCP.md](Phase-1-Read-Only-Catalogue-and-MCP.md), [Phase-2-Agent-Loop-and-Tracing.md](Phase-2-Agent-Loop-and-Tracing.md), [Phase-3-Evidence-Ledger-and-Reliable-Outputs.md](Phase-3-Evidence-Ledger-and-Reliable-Outputs.md), [Phase-4-RAG-Module-with-pgvector.md](Phase-4-RAG-Module-with-pgvector.md), and [Phase-5-Layered-Evaluation-Strategy.md](Phase-5-Layered-Evaluation-Strategy.md).

---

## 1. Purpose, Scope, and System Boundaries

### Goal
Provide a production-ready, security-hardened containerization and deployment architecture for the **PublishingTracker ecosystem**: `PublishingTracker.Api` (hosting REST endpoints and Streamable HTTP `/mcp`), `PublishingTracker.Agent` (hosting `POST /api/agent/query`, the two-stage verified loop, RAG vector retrieval, and `CompositeToolProvider`), and `PostgreSQL + pgvector` (`pgvector/pgvector:pg17`), with centralized OpenTelemetry export, readiness/liveness health checks, secrets management, and automated database migrations.

```
                               ┌────────────────────────────────────────────────────────┐
                               │                    Reverse Proxy /                     │
                               │                Ingress / API Gateway                   │
                               └───────────┬────────────────────────────────┬───────────┘
                                           │                                │
                                           │ TLS Termination                │ TLS Termination
                                           │ Path: /mcp, /api/*             │ Path: /api/agent/*
                                           ▼                                ▼
                     ┌──────────────────────────────────┐     ┌──────────────────────────────────┐
                     │ Container: publishingtracker-api │     │ Container: publishingtracker-agt │
                     │   • .NET 10 Chiseled / Non-root  │     │   • .NET 10 Chiseled / Non-root  │
                     │   • Port: 8080 (HTTP)            │     │   • Port: 8080 (HTTP)            │
                     │   • Hosted /mcp Streamable HTTP  │     │   • Two-Stage Agent Loop         │
                     │   • Health: /health (readiness)  │     │   • Health: /health (readiness)  │
                     └─────────────────┬────────────────┘     └─────────────────┬────────────────┘
                                       │                                        │
                                       │ PerRequestBearerHandler                │
                                       │ Internal network connection            │
                                       └────────────────────────────────────────┤
                                                                                │
                                        ┌───────────────────────────────────────┴───────────────────┐
                                        │                                                           │
                                        ▼                                                           ▼
                      ┌───────────────────────────────────┐                       ┌───────────────────────────────────┐
                      │ Container: postgres (pgvector)    │                       │ OpenTelemetry Collector / OTLP    │
                      │   • pgvector/pgvector:pg17        │                       │   • Azure App Insights / Grafana  │
                      │   • Port: 5432                    │                       │   • Metrics, Traces & Redacted Logs
                      │   • Relational Catalog + RAG      │                       └───────────────────────────────────┘
                      └───────────────────────────────────┘
```

### Architectural Decisions & Invariants
1. **Container Isolation & Single Responsibility:**
   - `PublishingTracker.Api` and `PublishingTracker.Agent` run in separate container instances sharing a common container network and PostgreSQL instance (`ADR-001`).
2. **Minimal Surface & Non-Root Execution:**
   - Docker images build on `.NET 10` SDK and execute on minimal, hardened `.NET 10 Chiseled` runtime images (`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`), running strictly under non-root users (`UID 1654:1654`) with read-only root filesystems and a mounted volume/tmpfs for `/tmp` and `temp_uploads`.
3. **Container-to-Container Transport Policy (Decision 1 Resolution):**
   - **Code Changes in Step 6.2:**
     - Add `AllowInsecureInternalHttp` (bool) to `AIProviderOptions.cs`.
     - Consolidate the TLS-allowlist logic into a single authoritative shared helper (e.g. `AIProviderOptions.IsEndpointTrusted(Uri endpoint, IHostEnvironment env)`), eliminating drift between `IsMcpEndpointSecure()` in `Program.cs` and `PerRequestBearerHandler.ValidateDestination()`.
     - Allow plain HTTP *only* if `AllowInsecureInternalHttp == true` AND the endpoint host matches an allowlisted internal container host (e.g., `api`), AND `environment.IsDevelopment() || environment.IsEnvironment("Testing")`.
   - **Production & Staging Guard:** Following the strict allow-list convention in `PerRequestBearerHandler.cs`, startup validation **fails fast and aborts startup** if `AllowInsecureInternalHttp == true` in any environment **outside Development or Testing** (blocking `Production`, `Staging`, etc.).
   - **Production Requirement:** In production and staging, container-to-container calls must use real TLS (`https://...`), provided by service mesh mTLS or TLS termination.
4. **Database Migration Lifecycle (Decision 2 Resolution):**
   - **Code Changes in Step 6.2:** In `PublishingTracker.Api/Program.cs`, replace the hardcoded `app.Environment.IsDevelopment()` migration check with `builder.Configuration.GetValue<bool>("Database:MigrateOnStartup", builder.Environment.IsDevelopment())` so that `Database:MigrateOnStartup=true` explicitly governs startup migrations in any environment.
   - **Startup Synchronization:** `PublishingTracker.Agent` does **not** execute schema migrations. On startup, the Agent verifies database readiness; if required tables (`rag_document_chunks`) are absent, it fails fast with a clear diagnostic message.
   - **Docker Compose Dependency:** In `docker-compose.yml`, the `agent` service depends on `api` with `condition: service_healthy`, ensuring the Api has completed migrations and its health endpoint returns healthy before the Agent attempts to boot or seed the RAG corpus.
5. **Standardized Health & Readiness Probes:**
   - **Code Changes in Step 6.3:** 
     - In `PublishingTracker.Api/Program.cs`: Evolve the existing flat `MapHealthChecks("/health")` to tagged endpoints: `/health/live` (process responsiveness) and `/health/ready` (deep check: database connectivity + `temp_uploads` writability).
     - In `PublishingTracker.Agent/Program.cs`: Register `AddHealthChecks()` and map `/health/live` and `/health/ready` (checking database connectivity and a non-authenticated TCP ping/anonymous probe to the API).
   - In chiseled containers (which lack curl/wget/shell), a dedicated lightweight standalone console tool `tools/PublishingTracker.HealthProbe` is compiled during the build stage and copied to `/app/healthcheck` (with executable permissions), invoked via `CMD ["/app/healthcheck"] http://localhost:8080/health/ready`.
6. **Graceful Shutdown & Budget Alignment:**
   - Align shutdown durations: `HostOptions.ShutdownTimeout` is set to `75s` (exceeding `AgentLoopOptions.OverallTimeout = 60s`), and compose `stop_grace_period` is set to `80s` so in-flight queries complete cleanly without mid-turn SIGKILL corruption.
7. **RAG Corpus Packaging:**
   - `docs/rag-corpus/` is copied into the publish build artifact of `PublishingTracker.Agent` so startup seeding (`Rag:AutoSeedOnStartup`) has access to all markdown guides inside the container filesystem.

---

## 2. Docker Architecture & Multi-Stage Builds

### 2.1 Multi-Stage Dockerfile Strategy
Both services utilize Docker multi-stage builds rooted in the solution directory (`Publishing-Tracker/apps/api/`):

1. **Build Stage (`mcr.microsoft.com/dotnet/sdk:10.0`):**
   - Restores dependencies using solution-level `Directory.Build.props`, `Directory.Packages.props`, and `.csproj` files to optimize Docker cache layer reuse.
   - Compiles target service with `-c Release` and `/p:UseSharedCompilation=false`.
   - Compiles `tools/PublishingTracker.HealthProbe` into `/build/healthcheck` as an AOT or trimmed lightweight console executable.
2. **Publish Stage:**
   - Executes `dotnet publish` targeting `net10.0` with `--no-restore` and `-o /app/publish`.
   - Copies `docs/rag-corpus/` into `/app/publish/docs/rag-corpus/` for the Agent.
   - Copies `/build/healthcheck` into `/app/publish/healthcheck`.
3. **Runtime Stage (`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`):**
   - Copies `/app/publish` to `/app`.
   - Runs as non-root user `app` (`1654:1654`).
   - Exposes container port `8080`.
   - Sets `ASPNETCORE_URLS=http://+:8080` and `DOTNET_EnableDiagnostics=0`.

---

## 3. Local & Staging Orchestration (`Publishing-Tracker/apps/api/docker-compose.yml`)

The compose stack at `Publishing-Tracker/apps/api/docker-compose.yml` orchestrates the local services:

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg17
    container_name: publishingtracker-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: publishingtracker_dev
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d publishingtracker_dev"]
      interval: 5s
      timeout: 5s
      retries: 10

  api:
    build:
      context: .
      dockerfile: src/PublishingTracker.Api/Dockerfile
    container_name: publishingtracker-api
    restart: unless-stopped
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=publishingtracker_dev;Username=postgres;Password=postgres"
      Jwt__Key: "${JWT_SIGNING_KEY:?JWT_SIGNING_KEY must be set (min 32 chars)}"
      Jwt__Issuer: "PublishingTracker.Api"
      Jwt__Audience: "PublishingTracker.Api"
      Database__MigrateOnStartup: "true"
    ports:
      - "7285:8080"
    volumes:
      - temp_uploads:/app/temp_uploads
    healthcheck:
      test: ["CMD", "/app/healthcheck"] # compiled in publish stage (or orchestrator HTTP probe)
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s

  agent:
    build:
      context: .
      dockerfile: src/PublishingTracker.Agent/Dockerfile
    container_name: publishingtracker-agent
    restart: unless-stopped
    stop_grace_period: 80s
    depends_on:
      api:
        condition: service_healthy
      postgres:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "/app/healthcheck"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=publishingtracker_dev;Username=postgres;Password=postgres"
      Jwt__Key: "${JWT_SIGNING_KEY:?JWT_SIGNING_KEY must be set (min 32 chars)}"
      Jwt__Issuer: "PublishingTracker.Api"
      Jwt__Audience: "PublishingTracker.Api"
      AIProvider__McpEndpoint: "http://api:8080/mcp"
      AIProvider__AllowInsecureInternalHttp: "true"
      AIProvider__Type: "${AI_PROVIDER_TYPE:-openrouter}"
      AIProvider__ApiKey: "${OPENROUTER_API_KEY}"
      AIProvider__OpenRouterApiKey: "${OPENROUTER_API_KEY}"
      Rag__AutoSeedOnStartup: "true"
    ports:
      - "7290:8080"

volumes:
  postgres_data:
  temp_uploads:
```

---

## 4. Step-by-Step Implementation Runbook (`Steps 6.1`–`6.4`)

```
Step 6.1: Dockerfiles, .dockerignore & Multi-Stage Images
   │  • Author src/PublishingTracker.Api/Dockerfile (chiseled non-root)
   │  • Author src/PublishingTracker.Agent/Dockerfile (chiseled non-root + rag-corpus copy)
   │  • Add .dockerignore covering bin, obj, git, .vs, .cr, and local temp files
   ▼
Step 6.2: Compose Stack, Migration Gating & Config Alignment
   │  • Update docker-compose.yml with API, Agent, Postgres & named volumes
   │  • Align config keys: AIProvider:McpEndpoint, AIProvider:AllowInsecureInternalHttp
   │  • Ensure Api performs migration and Agent waits for healthy status
   ▼
Step 6.3: Health Probes, Shutdown Budgets & Security Guards
   │  • Standardize /health probes checking DB connectivity and MCP reachability
   │  • Add production guard: fail fast if AllowInsecureInternalHttp is set in Production
   │  • Configure HostOptions.ShutdownTimeout (75s) and stop_grace_period (80s)
   ▼
Step 6.4: Environment Configuration, Secret Management & Operations Runbook
   │  • Configure Azure Container Apps / AWS ECS deployment manifests
   │  • Document production runbooks, secrets handling, and rate limiting recommendations
   │  • Verify full stack cold boot from clean git clone
```

---

## 5. Work Package Specifications

### Step 6.1 — Dockerfiles, .dockerignore & Multi-Stage Images
- **Goal:** Create standardized, secure Dockerfiles for both `.NET 10` ASP.NET Core services, plus a shared health probe.
- **Deliverables:**
  - `Publishing-Tracker/apps/api/src/PublishingTracker.Api/Dockerfile`
  - `Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Dockerfile`
  - `Publishing-Tracker/apps/api/tools/PublishingTracker.HealthProbe/` (single-file minimal C# console tool using `HttpClient` to probe HTTP status codes and exit 0 or 1)
  - `Publishing-Tracker/apps/api/.dockerignore` (excluding build caches, `.vs/`, `.cr/`, but retaining `docs/rag-corpus/`)
  - Images run under UID 1654:1654 without root permissions.

### Step 6.2 — Compose Stack, Migration Gating & Config Alignment
- **Goal:** Expand `docker-compose.yml` to spin up the complete local environment and verify service discovery.
- **Deliverables:**
  - Multi-container `docker-compose.yml` using correct `AIProvider:McpEndpoint` configuration and `stop_grace_period: 80s`.
  - Consolidated TLS allowlist helper `AIProviderOptions.IsEndpointTrusted(...)` shared between `Program.cs` and `PerRequestBearerHandler.cs`.
  - Startup synchronization: Api applies EF migrations; Agent waits for Api healthy check.
  - Verification that Agent queries resolve catalogue via internal Docker network (`http://api:8080/mcp`) when permitted in Development/Testing.

### Step 6.3 — Health Probes, Shutdown Budgets & Security Guards
- **Goal:** Equip services with Kubernetes/Container Apps compatible health checks and shutdown budgets.
- **Deliverables:**
  - Deep `/health` readiness check verifying DB connection and MCP endpoint reachability.
  - Startup validation: Fail fast if `AllowInsecureInternalHttp` is enabled outside Development/Testing.
  - Shutdown budget configured to 75s ensuring in-flight queries terminate cleanly.

### Step 6.4 — Environment Configuration & Production Readiness Runbook
- **Goal:** Document production deployment patterns (Neon PostgreSQL + Azure Container Apps / AWS ECS) and secrets handling.
- **Deliverables:**
  - Production deployment runbook in `docs/Deployment-and-Operations-Guide.md`.
  - Environment variable mapping table and mandatory JWT key length checks.
  - End-to-end containerized smoke test.

---

## 6. Acceptance Criteria & Quality Gates

1. **Clean Cold Start:** `docker compose up --build` launches the complete stack from a clean checkout, auto-applies database migrations, and answers `POST /api/agent/query`.
2. **Zero Root Privilege:** All application containers run under non-root user IDs without writable root filesystems.
3. **No Insecure HTTP in Production:** Any deployment with `ASPNETCORE_ENVIRONMENT=Production` attempting to use plain HTTP MCP endpoints fails immediately on startup.
4. **Docs Updated:** Master project plan, design log, and operational guides updated to reflect production deployment readiness.
