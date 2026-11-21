# PublishingTracker — Deployment, Containerization, & Operations Guide

Production and operational guide for containerizing, deploying, and operating the **PublishingTracker ecosystem**:
- **`PublishingTracker.Api`**: ASP.NET Core REST endpoints and hosted Streamable HTTP `/mcp` server.
- **`PublishingTracker.Agent`**: Authenticated two-stage agent orchestrator (`POST /api/agent/query`), semantic retrieval (`PublishingTracker.Rag`), and `CompositeToolProvider`.
- **`PostgreSQL + pgvector`**: Relational book/edition catalog and vector embedding store (`vector(1536)`).
- **`PublishingTracker.HealthProbe`**: Minimal AOT-ready native health probe executable for chiseled containers.

---

## 1. System Architecture & Topology

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
                     │   • Health: /health/ready        │     │   • Health: /health/ready        │
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

---

## 2. Hardened Chiseled Container Architecture

Both services use multi-stage builds rooted in `Publishing-Tracker/apps/api/`:
- **Build Stage:** `mcr.microsoft.com/dotnet/sdk:10.0` compiles the target service and `tools/PublishingTracker.HealthProbe`.
- **Runtime Stage:** `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` provides a minimal Ubuntu Noble base stripped of shells (`/bin/sh`, `/bin/bash`), package managers (`apt`, `dpkg`), and curl/wget utilities.

### Security Attributes
1. **Non-Root Execution:** Runs strictly under UID `1654:1654` (`app`).
2. **Read-Only Root Filesystem:** Root filesystem can be mounted read-only (`read_only: true`), with writable `/tmp` (tmpfs) and `/app/temp_uploads` (mounted volume).
3. **No Shell Surface:** Eliminates shell escape vectors and arbitrary script execution inside containers.

---

## 3. Health & Readiness Probing (`PublishingTracker.HealthProbe`)

Because chiseled containers lack `curl` and `wget`, healthchecks are performed using the lightweight `PublishingTracker.HealthProbe` utility compiled during the Docker build stage and placed at `/app/healthcheck`.

### Health Endpoints
- **`/health`**: Flat aggregate health status across all registered checks.
- **`/health/live`**: Process liveness probe (returns HTTP 200 immediately if process is responsive).
- **`/health/ready`**: Readiness probe checking:
  - `PublishingTracker.Api`: Database connection status + `temp_uploads` storage availability.
  - `PublishingTracker.Agent`: Database connection status (`database`) + MCP API host `/health/live` reachability (`mcp-api`).

### HealthProbe Usage
```bash
# Probe default readiness URL (http://localhost:8080/health/ready, 5s timeout)
/app/healthcheck

# Probe specific URL with custom timeout (in seconds)
/app/healthcheck http://localhost:8080/health/ready 5
```
**Exit Codes:**
- `0`: HTTP 2xx response received within timeout.
- `1`: Non-2xx response, network failure, or timeout.

---

## 4. Network Security & Internal Transport Policy

### `AIProvider:AllowInsecureInternalHttp` Guard
- In development/testing environments, internal Docker network communication between containers (e.g., `http://api:8080/mcp`) is permitted when `AIProvider__AllowInsecureInternalHttp=true`.
- **Production Guard (`ADR-011`):** Startup validation strictly prohibits `AllowInsecureInternalHttp` outside `Development` or `Testing`. Attempting to set `AllowInsecureInternalHttp=true` with `ASPNETCORE_ENVIRONMENT=Production` causes immediate startup termination.
- In production, internal calls must route through TLS endpoints (via reverse proxy, service mesh mTLS, or internal certificates).

### Destination Trust Evaluation (`AIProviderOptions.IsEndpointTrusted`)
- **HTTPS:** Always trusted across all environments.
- **HTTP Loopback (`localhost`, `127.0.0.1`):** Trusted in `Development` and `Testing` only.
- **HTTP Internal Container Hosts (`api`, `publishingtracker-api`):** Trusted only when `AllowInsecureInternalHttp=true` AND environment is `Development` or `Testing`.
- **All other HTTP destinations:** Strictly rejected with `InvalidOperationException`.

### `Jwt:Key` Minimum Entropy & OpenTelemetry OTLP Export
- Both `PublishingTracker.Api` and `PublishingTracker.Agent` fail fast on startup if `Jwt:Key` is shorter than **32 UTF-8 bytes (256 bits)** for HMAC-SHA256.
- Setting `OTEL_EXPORTER_OTLP_ENDPOINT` enables out-of-process OTLP span export in `PublishingTracker.Agent` (`EnableSensitiveData = false`).

---

## 5. Startup Synchronization & Database Migrations

### Startup Lifecycle
1. **PostgreSQL** starts and satisfies the healthcheck probe (`pg_isready -U postgres -d publishingtracker_dev`).
2. **`PublishingTracker.Api`** boots:
   - Evaluates `Database:MigrateOnStartup` (defaults to `Environment.IsDevelopment()`, or overridden via environment variable).
   - If enabled, runs `db.Database.Migrate()` to create/update relational tables and pgvector extension.
   - Becomes healthy once database connectivity is verified.
3. **`PublishingTracker.Agent`** boots:
   - Configured with `depends_on: { api: { condition: service_healthy }, postgres: { condition: service_healthy } }`.
   - Never runs migrations directly (single authoritative migration owner).
   - Immediately verifies PostgreSQL schema readiness (`rag_document_chunks` query) outside `Testing` and fails fast with a clear diagnostic error if migrations are missing.
   - If `Rag:AutoSeedOnStartup=true`, reads markdown guides from `/app/docs/rag-corpus` and idempotently seeds vector embeddings into PostgreSQL.

### Shutdown Alignment
- `HostOptions.ShutdownTimeout` is set to **`75s`** in `PublishingTracker.Agent`.
- Docker Compose `stop_grace_period` is set to **`80s`**.
- This guarantees in-flight agent loops (bounded by `AgentLoopOptions.OverallTimeout = 60s`) terminate gracefully without SIGKILL corruption.

---

## 6. Local Docker Compose Stack

Start the complete local stack from the repository root:

```powershell
# Set optional secrets
$env:JWT_SIGNING_KEY="YourSecureDevelopmentSecretKeyAtLeast32BytesLong!"
$env:OPENROUTER_API_KEY="<your-openrouter-key>"

# Launch the stack
docker compose -f Publishing-Tracker/apps/api/docker-compose.yml up --build
```

### Services & Port Mappings
| Service | Container Name | Host Port | Internal Port | Description |
|---|---|---|---|---|
| PostgreSQL | `publishingtracker-postgres` | `5435` (default, or `POSTGRES_PORT`) | `5432` | PostgreSQL 17 with `pgvector` extension |
| API | `publishingtracker-api` | `7285` | `8080` | REST API + `/mcp` Streamable HTTP endpoint |
| Agent | `publishingtracker-agent` | `7290` | `8080` | Natural language agent endpoint (`/api/agent/query`) |

---

## 7. Operations Runbook

### 7.1 Cold Boot Verification
```bash
# 1. Verify all containers are healthy
docker compose ps

# 2. Check API health endpoint
curl -i http://localhost:7285/health/ready

# 3. Check Agent health endpoint
curl -i http://localhost:7290/health/ready

# 4. Obtain JWT from API
TOKEN=$(curl -s -X POST http://localhost:7285/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"author-a@example.test","password":"Password123!"}' | jq -r .token)

# 5. Query Agent endpoint
curl -s -X POST http://localhost:7290/api/agent/query \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"question":"Which audiobooks are missing metadata?"}' | jq .
```

### 7.2 Zero-Downtime Rolling Update (Azure Container Apps / ECS)
1. Deploy new revision of `PublishingTracker.Api`.
2. Traffic shifts only after `/health/ready` probe succeeds.
3. Deploy new revision of `PublishingTracker.Agent`.
4. Agent container drains in-flight queries within the 75s shutdown grace window.

### 7.3 Secret Rotation (JWT Signing Key)
1. Generate new 256-bit HMAC key.
2. Update key in secrets vault (Azure Key Vault / AWS Secrets Manager).
3. Restart `PublishingTracker.Api` and `PublishingTracker.Agent` simultaneously.
4. Clients re-authenticate at `/api/auth/login` to obtain fresh tokens with the new key.
