# Phase 5 — Expanded Design: Layered Evaluation Strategy & Continuous Verification

> [!NOTE]
> **Status: Implemented & Verified.**
> All 4 steps (Steps 5.1 through 5.4) are complete. `PublishingTracker.Evals` contains 39 passing tests (30 Layer 1 deterministic tests, 4 Layer 2 real PostgreSQL + `pgvector` integration tests, and 5 Layer 3 `Category=LiveEval` baseline/smoke tests), out of 172 total tests across the solution.

Companion phase specification to [MCP-Agent-Project-Plan.md](../MCP-Agent-Project-Plan.md), [MCP-Agent-Design-Log.md](MCP-Agent-Design-Log.md), [Phase-0-Baseline-and-Contracts.md](Phase-0-Baseline-and-Contracts.md), [Phase-1-Read-Only-Catalogue-and-MCP.md](Phase-1-Read-Only-Catalogue-and-MCP.md), [Phase-2-Agent-Loop-and-Tracing.md](Phase-2-Agent-Loop-and-Tracing.md), [Phase-3-Evidence-Ledger-and-Reliable-Outputs.md](Phase-3-Evidence-Ledger-and-Reliable-Outputs.md), and [Phase-4-RAG-Module-with-pgvector.md](Phase-4-RAG-Module-with-pgvector.md).

---

## 1. Purpose, Scope, and System Boundaries

### Goal
Establish a **continuous, multi-tiered evaluation harness (`PublishingTracker.Evals`)** that validates model behavior, tool invocation accuracy, tenant isolation, provenance preservation, and budget enforcement across both offline deterministic suites and opt-in live provider smoke tests (`ADR-006`, `ADR-007`, `ADR-008`). Ensure that agent modifications, prompt iterations, or model version changes never degrade output validity, leak multi-tenant boundaries, or violate strict JSON/byte budgets.

```
┌──────────────────────────────────────────────────────────────────────────────────────────────┐
│                                 Continuous Evaluation Pyramid                                │
├──────────────────────────────────────────────────────────────────────────────────────────────┤
│  Layer 3: Live Provider Smoke & Quality Evals (Opt-In / Nightly / Non-Fork Only)             │
│    • Pinned OpenRouter / OpenAI / Claude Live Runs (Temp = 0.0, Seed = 42)                   │
│    • Multi-step tool grounding, RAG retrieval quality, & Stage 2 JSON schema compliance      │
│    • Zero data collection verification (data_collection: deny header asserted)              │
│    • Gated behind explicit API key check; skips cleanly if missing (never false fail/pass)   │
├──────────────────────────────────────────────────────────────────────────────────────────────┤
│  Layer 2: Multi-Tenant Integration & Real pgvector DB Isolation Evals (CI Fast Path)         │
│    • Real PostgreSQL + pgvector Test Container / CI Service (exercises IsNpgsql() SQL path) │
│    • Vector cosine ranking (<=>) vs. C# CosineDistance parity assertion                      │
│    • In-memory WebAppFactory wired to Agent via internal TestServer handler                  │
│    • Two-User cross-tenant boundary verification (Catalogue + RAG chunks + JWT negative mat) │
│    • Concurrent user interleaving (N=20 parallel turns across User A and B)                  │
├──────────────────────────────────────────────────────────────────────────────────────────────┤
│  Layer 1: Deterministic Orchestration & Provenance Evals (Unit / 0 Spend)                    │
│    • ScriptedChatClient + StubToolProvider + RecordingHttpMessageHandler                     │
│    • Dual budget exhaustion, repeat-call canonicalization, timeout aborts                    │
│    • EvidenceLedger indexing, zero-citation enforcement, prompt-injection defense           │
│    • Golden trajectory assertions against checked-in baseline scorecard                      │
└──────────────────────────────────────────────────────────────────────────────────────────────┘
```

### Key Architectural Invariants & Hardening Adjustments
1. **Real `pgvector` SQL Path & CI Hard-Fail Gate:**
   - Unlike Phase 4 unit tests (which run offline against EF Core `InMemoryDatabase` when Postgres is absent), Layer 2 explicitly executes the `IsNpgsql()` branch of `PublishingTrackerDbContext.cs` (which implements `IRagChunkStore.SearchByVectorAsync`), verifying the formatted vector SQL literal, `<=>` cosine distance sorting, and the SQL-level `OwnerUserId` filter.
   - **Hard-Fail Requirement:** Layer 2 tests must **hard-fail** (not degrade to InMemory or skip) if PostgreSQL with `pgvector` is unavailable. This test suite relies on the repository's established `TEST_POSTGRES_CONNECTION` environment variable (already wired into `.github/workflows/api.yml`), avoiding redundant or conflicting provisioning tools.
2. **Zero Unintentional API Spend in CI:** The default CI test workflow executes Layer 1 and Layer 2 suites exclusively using deterministic doubles and local PostgreSQL fixtures. Layer 3 tests are gated behind explicit traits (`[Trait("Category", "LiveEval")]`) requiring configured API secrets.
3. **Live Eval Security & PR Policy:** **Live evals never execute on pull requests from forks** to prevent API key exfiltration. Live tests require explicit opt-in (`RUN_LIVE_EVALS=true`), enforce a per-run cost cap ($0.50 max), and handle provider rate limits (429/5xx) with bounded exponential backoff.
4. **Structural Provenance vs. Hallucination Boundary (`ADR-007`):**
   - The metric is strictly defined as **Structural Provenance Rate (100% Required)**: Every cited `bookId`, `(bookId, editionId)` pair, field name, and `(documentId, chunkId)` pair must match the per-run `EvidenceLedger`.
   - Free-text semantic entailment (whether the model's natural language `answer` or `claim` is factually sound) is scored in Layer 3 via an advisory LLM-judge rubric, never acting as a hard CI failure gate.
5. **Deterministic Golden Scorecards:** Golden trajectory evaluations are compared against a checked-in baseline (`eval_baseline.json`). Any regression in tool selection accuracy, turn count, or provenance fidelity fails the evaluation suite.

---

## 2. Layered Evaluation Architecture

### 2.1 Layer 1: Deterministic Orchestration & Boundary Evals (In-Memory)
- **Assembly:** `Publishing-Tracker/apps/api/tests/PublishingTracker.Evals` (Unit).
- **Tooling:** `ScriptedChatClient`, `StubToolProvider`, `DeterministicTestEmbeddingGenerator`.
- **Target Coverage:**
  - Dual budgets (`MaxModelTurns = 6`, `MaxTotalToolCalls = 8`, `MaxPaginationPages = 2`, `RepeatCallLimit = 2`).
  - Strict JSON format & Stage 2 repair loop exhaustion (`VALIDATION_RETRIES_EXHAUSTED`).
  - Structural error code mapping across all 18 `AgentErrorCode` public categories.
  - Byte bounds (`<= 32,768` UTF-8 bytes) and duplicate key rejections.
  - Prompt-injection immunity inside tool/retrieval payloads.
  - **Indirect injection defense:** User notes containing injection payloads (e.g. attempting to override instructions or forge citations) cannot invoke unregistered tools or resolve invalid chunk citations.

### 2.2 Layer 2: Multi-Tenant Integration & Isolation Evals (Host + Real pgvector)
- **Assembly:** `Publishing-Tracker/apps/api/tests/PublishingTracker.Evals` (Integration).
- **Tooling:** `TestWebAppFactory`, `HttpClientTransport`, real `McpClient`, PostgreSQL 17 + `pgvector` test container via `TEST_POSTGRES_CONNECTION`.
- **Wiring Architecture:**
  - `PublishingTracker.Api` runs under `TestWebAppFactory<PublishingTracker.Api.Program>`.
  - `PublishingTracker.Agent` is instantiated in the test harness with its `StreamableHttpMcpToolProvider` configured with an `HttpMessageHandler` pointing directly to the Api's in-memory `TestServer.CreateHandler()`.
  - Both services point to the same PostgreSQL test database specified by `TEST_POSTGRES_CONNECTION`. If unset or unreachable, Layer 2 tests **fail immediately**.
- **Target Coverage:**
  - **`pgvector` Operator Parity:** Asserts that PostgreSQL `<=>` vector ordering in `PublishingTrackerDbContext.cs` matches `VectorMath.CosineDistance` ranking across a test set of 20 document chunks.
  - **Cross-Tenant Isolation:** Proving User A never sees User B's titles via `/mcp` or Agent Query, even when User A explicitly specifies User B's book IDs or note titles in the prompt.
  - **Concurrent Interleaved Users:** N=20 parallel requests alternating User A and User B asserting zero cross-contamination in answers, ledger state, or RAG hits.
  - **Mid-Run Token Expiry:** A token expiring mid-run during a multi-turn tool call produces an expected `TOOL_ERROR` or `UNAUTHORIZED` without leaking secrets.
  - **JWT Negative Matrix:** Tests against invalid issuer, audience, expired lifetime, missing `iat`, `alg=none`, and non-positive integer user IDs.

### 2.3 Layer 3: Live Smoke & Trajectory Quality Evals (Opt-In / Nightly)
- **Assembly:** `Publishing-Tracker/apps/api/tests/PublishingTracker.Evals` (Live).
- **Filter:** `[Trait("Category", "LiveEval")]`.
- **Tooling:** Real `OpenRouter` or `OpenAI` client endpoints using `AIProviderSetup.CreateChatClient`.
- **Target Coverage:**
  - Trajectory evaluation on canonical multi-step prompts:
    - *"Find my incomplete audiobooks and inspect what metadata is missing."*
    - *"What are the formatting guidelines for ebook file sizes and covers?"*
  - **Retrieval Quality (Recall@k / MRR):** Verifies that semantic guideline searches return the expected canonical document chunks in the top-3 hits.
  - **Privacy Header Compliance:** Network recording verifies that live requests send `HTTP-Referer`, `X-Title`, and `data_collection: deny`.

---

## 3. Step-by-Step Implementation Runbook (`Steps 5.1`–`5.4`)

```
Step 5.1: Eval Harness Foundation & Golden Trajectory Datasets
   │  • Create Publishing-Tracker/apps/api/tests/PublishingTracker.Evals/ project & sln registration
   │  • Implement EvalScenario, GoldenTrajectory, and EvaluationScorecard models
   │  • Define canonical evaluation scenarios (catalogue, RAG, mixed, adversarial)
   ▼
Step 5.2: Offline Trajectory, Provenance & Indirect Injection Evaluator
   │  • Implement TrajectoryEvaluator & ProvenanceScorer
   │  • Add deterministic eval suite verifying scoring against scripted runs
   │  • Add indirect prompt-injection and adversarial note fixtures
   ▼
Step 5.3: Real pgvector & Multi-Tenant Integration Scenarios
   │  • Wire to TEST_POSTGRES_CONNECTION (hard-fail if absent/down; no silent fallback)
   │  • Implement AgentMcpIntegrationEvalTests wiring Agent -> Api TestServer
   │  • Run concurrent two-user interleaving and mid-run token expiry suites
   ▼
Step 5.4: Live Smoke Test Runner, CI Quality Gates & Baseline Scorecard
   │  • Implement LiveSmokeEvalTests with [Trait("Category", "LiveEval")]
   │  • Add GitHub Actions workflow with fork protection and secret guards
   │  • Check in baseline scorecard (eval_baseline.json) and enforce regression gates
```

---

## 4. Work Package Specifications

### Step 5.1 — Eval Harness Foundation & Golden Trajectory Datasets
- **Goal:** Create the evaluation project structure and models to capture eval runs, tool trajectories, and expected assertions.
- **Deliverables:**
  - `PublishingTracker.Evals.csproj` added to `PublishingTracker.Api.sln`.
  - `EvalScenario` record: `Id`, `Description`, `Prompt`, `ExpectedTools`, `ForbiddenTools`, `ExpectedDocumentIds`, `ExpectAnswered`.
  - `GoldenDataset`: Static repository of 12 canonical benchmarks (Catalogue, RAG guidelines, cross-tenant attack, unanswerable queries).

### Step 5.2 — Offline Trajectory, Provenance & Indirect Injection Evaluator
- **Goal:** Implement deterministic evaluators that assess agent runs against golden expectations without network calls.
- **Deliverables:**
  - `TrajectoryEvaluator`: Scores tool call sequence, turn economy, and parameter validity.
  - `ProvenanceScorer`: Scores citation completeness and verifies zero uncorroborated claims.
  - Indirect prompt-injection tests proving ingested note text cannot trigger unauthorized tool execution.

### Step 5.3 — Real pgvector & Multi-Tenant Integration Scenarios
- **Goal:** Verify the complete stack (API + MCP + Agent + RAG) in integration tests with real PostgreSQL + `pgvector`.
- **Deliverables:**
  - PostgreSQL test fixture applying migrations and verifying `pgvector` `<=>` distance parity with C# `VectorMath`.
  - Wired in-memory test host allowing `PublishingTracker.Agent` to call `PublishingTracker.Api` `/mcp` via `TestServer.CreateHandler()`.
  - N=20 parallel concurrent user runs asserting zero cross-tenant contamination.

### Step 5.4 — Live Smoke Test Runner, CI Quality Gates & Baseline Scorecard
- **Goal:** Provide a safe, opt-in mechanism to test live models against the evaluation set and verify privacy compliance.
- **Deliverables:**
  - `LiveSmokeEvalTests.cs` runnable with `dotnet test --filter "Category=LiveEval"`.
  - OpenRouter request validation ensuring zero retention / deny headers.
  - Baseline scorecard (`eval_baseline.json`) with automated regression detection.

---

## 5. Acceptance Criteria & Quality Gates

1. **Deterministic Test Baseline:** All offline evaluation tests execute in standard CI with zero external API calls or token spend.
2. **Postgres Operator Parity:** The real pgvector `<=>` SQL path is verified against C# cosine distance in an automated test.
3. **100% Structural Provenance:** Zero tolerance for unverified citations; any hallucinated field or chunk fails the scorecard.
4. **Tenant Boundary Defense:** Cross-tenant attack scenarios consistently achieve 100% rejection rate.
5. **Safe Live CI Gates:** Live tests never run on fork PRs, skip gracefully when keys are omitted, and cap spend at $0.50.
