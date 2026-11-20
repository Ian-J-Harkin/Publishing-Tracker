# Phase 5 — Expanded Design: Layered Evaluation Strategy & Continuous Verification

Companion phase specification to [MCP-Agent-Project-Plan.md](../MCP-Agent-Project-Plan.md), [MCP-Agent-Design-Log.md](MCP-Agent-Design-Log.md), [Phase-0-Baseline-and-Contracts.md](Phase-0-Baseline-and-Contracts.md), [Phase-1-Read-Only-Catalogue-and-MCP.md](Phase-1-Read-Only-Catalogue-and-MCP.md), [Phase-2-Agent-Loop-and-Tracing.md](Phase-2-Agent-Loop-and-Tracing.md), [Phase-3-Evidence-Ledger-and-Reliable-Outputs.md](Phase-3-Evidence-Ledger-and-Reliable-Outputs.md), and [Phase-4-RAG-Module-with-pgvector.md](Phase-4-RAG-Module-with-pgvector.md).

---

## 1. Purpose, Scope, and System Boundaries

### Goal
Establish a **continuous, multi-tiered evaluation harness (`PublishingTracker.Evals`)** that validates model behavior, tool invocation accuracy, tenant isolation, provenance preservation, and budget enforcement across both offline deterministic suites and opt-in live provider smoke tests (`ADR-006`, `ADR-007`, `ADR-008`). Ensure that agent modifications, prompt iterations, or model version changes never degrade output validity, leak multi-tenant boundaries, or violate strict JSON/byte budgets.

```
┌──────────────────────────────────────────────────────────────────────────────────────────────┐
│                                 Continuous Evaluation Pyramid                                │
├──────────────────────────────────────────────────────────────────────────────────────────────┤
│  Layer 3: Live Provider Smoke & Quality Evals (Opt-In / Nightly)                             │
│    • Pinned OpenRouter / OpenAI / Claude Live Runs                                           │
│    • End-to-end multi-step tool grounding & Stage 2 JSON schema compliance                   │
│    • Zero data collection verification (data_collection: deny)                               │
├──────────────────────────────────────────────────────────────────────────────────────────────┤
│  Layer 2: Multi-Tenant Integration & DB Isolation Evals (CI Fast Path)                       │
│    • TestWebAppFactory (PostgreSQL + pgvector / InMemory) + Hosted /mcp                      │
│    • User A vs. User B cross-tenant boundary verification (Catalogue + RAG chunks)           │
│    • Real Streamable HTTP client & JWT hygiene validation (iat presence, expiry, 401/403)   │
├──────────────────────────────────────────────────────────────────────────────────────────────┤
│  Layer 1: Deterministic Orchestration & Provenance Evals (Unit / 0 Spend)                    │
│    • ScriptedChatClient + StubToolProvider + RecordingHttpMessageHandler                     │
│    • Dual budget exhaustion, repeat-call canonicalization, timeout aborts                    │
│    • EvidenceLedger indexing, zero-citation enforcement, prompt-injection defense           │
└──────────────────────────────────────────────────────────────────────────────────────────────┘
```

### Key Architectural Invariants
1. **Zero Unintentional API Spend in CI:** The default CI test workflow executes Layer 1 and Layer 2 suites exclusively using deterministic in-memory doubles and local PostgreSQL/InMemory fixtures. Layer 3 tests are gated behind explicit traits (`[Trait("Category", "LiveEval")]`) requiring configured API secrets.
2. **Deterministic Evaluation Golden Sets:** A versioned evaluation corpus of canonical queries with expected tool trajectories, required catalogue/document evidence links, and disallowed cross-tenant IDs.
3. **Automated Evaluation Metrics & Scorecards:**
   - **Tool Selection Accuracy:** Precision and recall of appropriate tool invocations (e.g., querying `find_titles_missing_metadata` before `get_title_details`).
   - **Provenance Fidelity (Zero Hallucination Rate):** Percentage of runs where 100% of cited book/edition IDs, fields, and chunk IDs are corroborated by the `EvidenceLedger`.
   - **Budget & Latency Adherence:** Conformance with `MaxModelTurns = 6`, `MaxTotalToolCalls = 8`, and per-call/overall timeouts.
   - **Tenant Isolation Rate:** 100% rejection rate for cross-tenant data requests.

---

## 2. Layered Evaluation Architecture

### 2.1 Layer 1: Deterministic Orchestration & Boundary Evals (In-Memory)
- **Assembly:** `tests/PublishingTracker.Agent.Tests` and `tests/PublishingTracker.Evals` (Unit).
- **Tooling:** `ScriptedChatClient`, `StubToolProvider`, `DeterministicTestEmbeddingGenerator`.
- **Target Coverage:**
  - Dual budgets (`MaxModelTurns`, `MaxTotalToolCalls`, `MaxPaginationPages`, `RepeatCallLimit`).
  - Strict JSON format & Stage 2 repair loop exhaustion (`VALIDATION_RETRIES_EXHAUSTED`).
  - Structural error code mapping across all 18 `AgentErrorCode` public categories.
  - Byte bounds (`<= 32,768` UTF-8 bytes) and duplicate key rejections.
  - Prompt-injection immunity inside tool/retrieval payloads.

### 2.2 Layer 2: Multi-Tenant Integration & Isolation Evals (Host + DB)
- **Assembly:** `tests/PublishingTracker.Api.Tests` and `tests/PublishingTracker.Evals` (Integration).
- **Tooling:** `TestWebAppFactory`, `HttpClientTransport`, real `McpClient`, in-process database (`Npgsql` / `pgvector` / `InMemory`).
- **Target Coverage:**
  - Cross-tenant catalog isolation: Proving User A never sees User B's titles via `/mcp` or Agent Query.
  - Cross-tenant RAG isolation: Proving User A never retrieves User B's private notes from `IRagChunkStore`.
  - JWT hygiene: Immediate HTTP 401 when `iat` is omitted or token is expired.
  - Full HTTP request lifecycle through ASP.NET Core middleware, authorization, and endpoint routing.

### 2.3 Layer 3: Live Smoke & Trajectory Quality Evals (Opt-In / Nightly)
- **Assembly:** `tests/PublishingTracker.Evals` (Live).
- **Filter:** `[Trait("Category", "LiveEval")]`.
- **Tooling:** Real `OpenRouter` or `OpenAI` client endpoints using `AIProviderSetup.CreateChatClient`.
- **Target Coverage:**
  - Trajectory evaluation on canonical multi-step prompts:
    - *"Find my incomplete audiobooks and inspect what metadata is missing."*
    - *"What are the formatting guidelines for ebook file sizes and covers?"*
  - Verification that the live LLM emits clean JSON conforming to `AgentModelDraft` schema.
  - Verification that OpenRouter privacy headers (`data_collection: deny`) are attached and acknowledged.

---

## 3. Step-by-Step Implementation Runbook (`Steps 5.1`–`5.4`)

```
Step 5.1: Eval Harness Foundation & Golden Trajectory Datasets
   │  • Create tests/PublishingTracker.Evals/ project & solution registration
   │  • Implement EvalScenario, GoldenTrajectory, and EvaluationScorecard models
   │  • Define canonical evaluation scenarios (catalogue, RAG, mixed, adversarial)
   ▼
Step 5.2: Offline Trajectory & Provenance Evaluator
   │  • Implement TrajectoryEvaluator & ProvenanceScorer
   │  • Add deterministic eval suite verifying scoring against scripted runs
   │  • Zero-spend execution in standard CI test pass
   ▼
Step 5.3: End-to-End Multi-Tenant Integration Scenarios
   │  • Implement AgentMcpIntegrationEvalTests using TestWebAppFactory
   │  • Full two-user lifecycle tests across combined MCP + RAG pipeline
   │  • Automated verification of non-model-controlled identity enforcement
   ▼
Step 5.4: Live Smoke Test Runner & CI Evaluation Matrix
   │  • Implement LiveSmokeEvalTests with [Trait("Category", "LiveEval")]
   │  • Add CI workflow configuration for automated eval reporting
   │  • Document operational runbook and failure troubleshooting
```

---

## 4. Work Package Specifications

### Step 5.1 — Eval Harness Foundation & Golden Trajectory Datasets
- **Goal:** Create the evaluation project structure and models to capture eval runs, tool trajectories, and expected assertions.
- **Deliverables:**
  - `PublishingTracker.Evals.csproj` added to `PublishingTracker.Api.sln`.
  - `EvalScenario` record: `Id`, `Description`, `Prompt`, `ExpectedTools`, `ForbiddenTools`, `ExpectedDocumentIds`, `ExpectAnswered`.
  - `GoldenDataset`: Static repository of ~12 canonical benchmarks (Catalogue, RAG guidelines, cross-tenant attack, unanswerable queries).

### Step 5.2 — Offline Trajectory & Provenance Evaluator
- **Goal:** Implement deterministic evaluators that assess agent runs against golden expectations without network calls.
- **Deliverables:**
  - `TrajectoryEvaluator`: Scores tool call sequence, turn economy, and parameter validity.
  - `ProvenanceScorer`: Scores citation completeness and verifies zero uncorroborated claims.
  - Deterministic evaluation test suite passing in `< 2s`.

### Step 5.3 — End-to-End Multi-Tenant Integration Scenarios
- **Goal:** Verify the complete stack (API + MCP + Agent + RAG) in integration tests with real HTTP/database plumbing.
- **Deliverables:**
  - Automated dual-user test runner verifying that User A calling `POST /api/agent/query` cannot access User B's catalog or notes even if the user prompt explicitly requests User B's book IDs or note titles.
  - Verification of `toolsUsed` duration and telemetry recording.

### Step 5.4 — Live Smoke Test Runner & CI Quality Gates
- **Goal:** Provide a safe, opt-in mechanism to test live models against the evaluation set and verify privacy compliance.
- **Deliverables:**
  - `LiveSmokeEvalTests.cs` runnable with `dotnet test --filter "Category=LiveEval"`.
  - OpenRouter request validation ensuring zero retention / deny headers.
  - Automated markdown scorecard generation for eval runs.

---

## 5. Acceptance Criteria & Quality Gates

1. **Deterministic Test Baseline:** All offline evaluation tests execute in standard CI with zero external API calls or token spend.
2. **100% Provenance Enforcement:** Zero tolerance for unverified citations; any hallucinated field or chunk fails the scorecard.
3. **Tenant Boundary Defense:** Cross-tenant attack scenarios consistently achieve 100% rejection rate.
4. **Complete Guide Alignment:** Update all user, testing, and programming guides upon completion of each phase step.
