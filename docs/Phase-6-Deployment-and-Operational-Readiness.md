# Phase 6 — Expanded Design: Deployment, Containerization, & Operational Readiness

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
                     │   • Health: /health/ready, /live │     │   • Health: /health/ready, /live │
                     └─────────────────┬────────────────┘     └─────────────────┬────────────────┘
                                       │                                        │
                                       │ PerRequestBearerHandler                │
                                       │ (Outbound JWT -> /mcp)                 │
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

### Architectural Invariants
1. **Container Isolation & Single Responsibility:**
   - `PublishingTracker.Api` and `PublishingTracker.Agent` run in separate container instances sharing a common container network and PostgreSQL instance (`ADR-001`).
2. **Minimal Surface & Non-Root Execution:**
   - Docker images build on `.NET 10` SDK and execute on minimal, hardened `.NET 10 Chiseled` runtime images (`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` or `alpine`), running strictly under non-root users (`UID 1654`).
3. **Multi-Environment Configuration & Safe Defaults:**
   - No hardcoded secrets, connection strings, or API tokens in image layers or source code.
   - Production targets Neon / Managed Cloud PostgreSQL; local/compose runs against `pgvector/pgvector:pg17`.
   - Ingestion seeding (`Rag:AutoSeedOnStartup`) runs once on application start or via job container, skipping when documents match existing SHA-256 hashes.
4. **Standardized Health & Readiness Probes:**
   - Both services expose `/health/live` (liveness) and `/health/ready` (readiness checking DB connectivity and model provider configuration).

---

## 2. Docker Architecture & Multi-Stage Builds

### 2.1 Multi-Stage Dockerfile Strategy
Both services utilize Docker multi-stage builds rooted in the solution directory (`Publishing-Tracker/apps/api/`):

1. **Build Stage (`mcr.microsoft.com/dotnet/sdk:10.0`):**
   - Restores dependencies using solution-level `Directory.Build.props`, `Directory.Packages.props`, and `.csproj` files to optimize Docker cache layer reuse.
   - Compiles with `-c Release` and `/p:UseSharedCompilation=false`.
2. **Publish Stage:**
   - Executes `dotnet publish` targeting `net10.0` with `--no-restore` and `-o /app/publish`.
3. **Runtime Stage (`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`):**
   - Copies `/app/publish` to `/app`.
   - Runs as non-root user `app` (`1654:1654`).
   - Exposes container port `8080`.
   - Sets `ASPNETCORE_URLS=http://+:8080` and `DOTNET_EnableDiagnostics=0`.

---

## 3. Local & Staging Orchestration (`docker-compose.yml`)

The root `docker-compose.yml` orchestrates the local stack:

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
      Jwt__Key: "${JWT_SIGNING_KEY:-dev-insecure-signing-key-minimum-32-chars-long}"
      Jwt__Issuer: "PublishingTracker.Api"
      Jwt__Audience: "PublishingTracker.Api"
    ports:
      - "7285:8080"
    healthcheck:
      test: ["CMD", "/app/PublishingTracker.Api", "--health-check"] # or curl/http probe
      interval: 10s
      timeout: 5s
      retries: 5

  agent:
    build:
      context: .
      dockerfile: src/PublishingTracker.Agent/Dockerfile
    container_name: publishingtracker-agent
    restart: unless-stopped
    depends_on:
      api:
        condition: service_started
      postgres:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=publishingtracker_dev;Username=postgres;Password=postgres"
      Jwt__Key: "${JWT_SIGNING_KEY:-dev-insecure-signing-key-minimum-32-chars-long}"
      Jwt__Issuer: "PublishingTracker.Api"
      Jwt__Audience: "PublishingTracker.Api"
      Mcp__ServerUrl: "http://api:8080/mcp"
      AIProvider__Type: "${AI_PROVIDER_TYPE:-openrouter}"
      AIProvider__ApiKey: "${OPENROUTER_API_KEY}"
      AIProvider__OpenRouterApiKey: "${OPENROUTER_API_KEY}"
      Rag__AutoSeedOnStartup: "true"
    ports:
      - "7290:8080"

volumes:
  postgres_data:
```

---

## 4. Step-by-Step Implementation Runbook (`Steps 6.1`–`6.4`)

```
Step 6.1: Dockerfiles & Multi-Stage Production Images
   │  • Author src/PublishingTracker.Api/Dockerfile (chiseled non-root)
   │  • Author src/PublishingTracker.Agent/Dockerfile (chiseled non-root)
   │  • Add .dockerignore covering bin, obj, git, and local temp files
   ▼
Step 6.2: Compose Stack & Automated Database Migration Lifecycle
   │  • Update Publishing-Tracker/apps/api/docker-compose.yml with API, Agent, Postgres
   │  • Configure startup migration strategy & EF Core bundle / auto-migrate on start
   │  • Validate inter-service communication (Agent -> http://api:8080/mcp)
   ▼
Step 6.3: Health Probes, Readiness Endpoints, & Graceful Shutdown
   │  • Add Microsoft.AspNetCore.Diagnostics.HealthChecks to Api and Agent
   │  • Map /health/live and /health/ready endpoints verifying DB & MCP reachability
   │  • Configure SIGTERM handling and CancellationToken propagation
   ▼
Step 6.4: Environment Configuration, Secret Management, & Production Guides
   │  • Configure Azure App Service / Container Apps & Neon deployment manifests
   │  • Update MCP User Guide, Testing Guide, and Deployment Runbooks
   │  • Verify full stack cold boot from clean git clone
```

---

## 5. Work Package Specifications

### Step 6.1 — Dockerfiles & Multi-Stage Production Images
- **Goal:** Create standardized, secure Dockerfiles for both `.NET 10` ASP.NET Core services.
- **Deliverables:**
  - `Publishing-Tracker/apps/api/src/PublishingTracker.Api/Dockerfile`
  - `Publishing-Tracker/apps/api/src/PublishingTracker.Agent/Dockerfile`
  - `Publishing-Tracker/apps/api/.dockerignore`
  - Verification that images compile, package, and execute under UID 1654 without root permissions.

### Step 6.2 — Compose Stack & Database Migration Lifecycle
- **Goal:** Expand `docker-compose.yml` to spin up the complete local environment and verify service discovery.
- **Deliverables:**
  - Multi-container `docker-compose.yml` with health checks and proper dependency startup ordering (`postgres -> api -> agent`).
  - Migration check: verifying `pgvector` extension and EF Core schema are applied cleanly on fresh boot.
  - Test verifying Agent query resolving catalogue via internal Docker DNS (`http://api:8080/mcp`).

### Step 6.3 — Health Probes, Readiness Endpoints, & Graceful Shutdown
- **Goal:** Equip services with Kubernetes/Container Apps compatible health checks.
- **Deliverables:**
  - `AddHealthChecks()` in `PublishingTracker.Api/Program.cs` and `PublishingTracker.Agent/Program.cs`.
  - `/health/live` (shallow ping) and `/health/ready` (deep check: DB connection + MCP endpoint connectivity).
  - Clean `IHostApplicationLifetime` graceful shutdown allowing inflight agent loops up to `AgentLoopOptions.OverallTimeout` (60s) to terminate cleanly.

### Step 6.4 — Environment Configuration & Production Readiness Runbook
- **Goal:** Document production deployment patterns (Neon PostgreSQL + Azure Container Apps / AWS ECS) and secrets handling.
- **Deliverables:**
  - Production deployment runbook in `docs/Deployment-and-Operations-Guide.md`.
  - Environment variable mapping table and secret rotation guidelines.
  - Final full-stack smoke test and verification.

---

## 6. Acceptance Criteria & Quality Gates

1. **Clean Cold Start:** `docker compose up --build` launches the complete stack from a clean checkout, auto-applies database migrations, and answers `POST /api/agent/query`.
2. **Zero Root Privilege:** All application containers run under non-root user IDs without writable root filesystems.
3. **Health Check Conformance:** Health probes report 200 OK only when dependencies are fully ready.
4. **Docs Updated:** Master project plan, design log, and operational guides updated to reflect production deployment readiness.
