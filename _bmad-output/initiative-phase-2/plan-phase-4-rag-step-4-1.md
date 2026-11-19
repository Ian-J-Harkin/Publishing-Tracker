---
title: 'Phase 4 RAG Step 4.1'
type: 'feature'
ticket: ''
created: '2026-11-19'
status: 'built'
baseline_revision: 'df15f423d63763b4cc7ddb5191c9910dc01419ff'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: [quick]
review_loop_iteration: 0
context:
  - 'docs/Phase-4-RAG-Module-with-pgvector.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Phase 4's RAG library foundation and its first deterministic test are not yet present, so later RAG work has no shared entity, contracts, chunk-store boundary, or vector validation primitives.

**Approach:** Implement only the Step 4.1 project scaffold, contracts/entity/vector utilities, test embedding generator, and Test #1 as specified in the phase document, then run the entire API solution test suite.

## Boundaries & Constraints

**Always:** Keep `PublishingTracker.Rag` dependent only on `PublishingTracker.Application` and `PublishingTracker.Domain`; preserve existing project and namespace conventions. Validate expected vector dimensions, finite values, and positive finite norm; provide the specified cosine and pgvector formatting helpers. Do not disturb unrelated existing untracked work.

**Never:** Implement Steps 4.2–4.6 or modify `PublishingTracker.Infrastructure`, `PublishingTracker.Agent`, or `EvidenceLedger`. Do not add ingestion, retrieval, pgvector persistence, query filters, tool integration, or additional Phase 4 tests.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| INVALID_DIMENSIONS | Vector has 1535 dimensions | Try validation returns false; validation throws | ArgumentException |
| ZERO_VECTOR | Expected dimensions, all zero | Try validation returns false; validation throws | ArgumentException |
| NON_FINITE_VECTOR | Expected dimensions containing NaN or infinity | Try validation returns false; validation throws | ArgumentException |
| VALID_VECTOR | Expected dimensions, finite and nonzero | Validation succeeds; self cosine distance is zero | No error expected |

</frozen-after-approval>

## Code Map

- `docs/Phase-4-RAG-Module-with-pgvector.md:80-107` -- Step 4.1 boundary, exact file list, and test gate; sections 108-403 give the prescribed implementations and Test #1.
- `Publishing-Tracker/apps/api/Directory.Build.props` -- Shared `net10.0`, nullable, and implicit-usings settings; new project inherits these.
- `Publishing-Tracker/apps/api/Directory.Packages.props` -- Centrally pins `Microsoft.Extensions.AI` 10.10.0; use the existing package without adding versions.
- `Publishing-Tracker/apps/api/PublishingTracker.Api.sln` -- Existing `src` solution folder and project registration convention; add the RAG library under `src`.
- `Publishing-Tracker/apps/api/src/PublishingTracker.Application/PublishingTracker.Application.csproj` and `.../PublishingTracker.Domain/PublishingTracker.Domain.csproj` -- Project-reference targets; RAG references both only.
- `Publishing-Tracker/apps/api/tests/PublishingTracker.Agent.Tests/PublishingTracker.Agent.Tests.csproj` -- Add the RAG project reference alongside the existing Agent reference.
- `Publishing-Tracker/apps/api/tests/PublishingTracker.Agent.Tests/Phase3EvidenceAndValidationTests.cs` -- Existing xUnit test conventions; Test #1 is the phase document's vector validation test.

## Tasks & Acceptance

**Execution:**
- [x] `Publishing-Tracker/apps/api/src/PublishingTracker.Rag/PublishingTracker.Rag.csproj`, `Publishing-Tracker/apps/api/PublishingTracker.Api.sln`, and `Publishing-Tracker/apps/api/tests/PublishingTracker.Agent.Tests/PublishingTracker.Agent.Tests.csproj` -- Add the library with its prescribed framework/package/project references, register it in the `src` solution folder, and reference it from Agent.Tests.
- [x] `Publishing-Tracker/apps/api/src/PublishingTracker.Rag/Entities/RagDocumentChunk.cs`, `.../Contracts/RagContracts.cs`, `.../Persistence/IRagChunkStore.cs`, and `.../Services/VectorMath.cs` -- Implement the Step 4.1 entity, DTOs, interface, and vector helpers per the specification.
- [x] `Publishing-Tracker/apps/api/tests/PublishingTracker.Agent.Tests/Doubles/DeterministicTestEmbeddingGenerator.cs` and `.../Phase4RagRetrievalAndIsolationTests.cs` -- Add the deterministic offline generator and exactly Test #1.

**Acceptance Criteria:**
- Given the API solution, when it is loaded and built, then the new RAG project is registered under `src`, references only Application and Domain, and Agent.Tests references RAG.
- Given invalid or valid vectors from the specified edge-case matrix, when Test #1 runs, then validation and cosine-distance assertions pass.
- Given the completed Step 4.1 changes, when `dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln --no-restore -m:1 /nodeReuse:false /p:UseSharedCompilation=false` runs, then all 97 tests pass, including 1/1 in `Phase4RagRetrievalAndIsolationTests`.

## Implementation Notes


## Plan Change Log


## Review Triage Log


## Verification

**Commands:**
- `dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln --no-restore -m:1 /nodeReuse:false /p:UseSharedCompilation=false` -- expected: 97/97 passing, including 1/1 Phase 4 test.

## Design Notes

The test double maps configured publishing-domain keywords to orthogonal dimensions and uses a stable SHA-256 fallback so arbitrary text has a nonzero deterministic vector without external API calls. Keep the Step 4.1 test to the exact invalid-dimension, zero, NaN, infinity, and valid self-distance assertions in the phase document.
