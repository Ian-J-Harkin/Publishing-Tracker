# Phase 3 — Expanded Design: Linked Evidence Ledger & Reliable Final Outputs

Companion phase specification to [MCP-Agent-Project-Plan.md](../MCP-Agent-Project-Plan.md), [MCP-Agent-Design-Log.md](MCP-Agent-Design-Log.md), [Phase-0-Baseline-and-Contracts.md](Phase-0-Baseline-and-Contracts.md), [Phase-1-Read-Only-Catalogue-and-MCP.md](Phase-1-Read-Only-Catalogue-and-MCP.md), and [Phase-2-Agent-Loop-and-Tracing.md](Phase-2-Agent-Loop-and-Tracing.md).

---

## 1. Purpose, Scope, and System Boundaries

### Goal
Upgrade `PublishingTracker.Agent` from free-text completion (Phase 2) to a **two-stage, schema-validated, provenance-verified response pipeline** (`ADR-007`, `ADR-009`). Every response returned by `POST /api/agent/query` must conform to the versioned `1.0` response envelope, use a closed `AgentErrorCode` taxonomy for failures, populate `toolsUsed` strictly from host execution records, validate pagination sequences and every cited `(bookId, editionId?, fields)` and `(documentId, chunkId)` entry against the per-run `EvidenceLedger`, enforce checkable truncation disclosure when pagination remains incomplete (`hasMore == true`), and allow at most `MaxValidationRetries = 2` bounded repair turns within `MaxModelTurns = 6`.

> **Provenance vs. Semantic Verification Boundary (`ADR-007`):**  
> Phase 3 validation proves **structural source and field provenance** against the run's `EvidenceLedger`—namely that every cited `bookId`, `(bookId, editionId)` pairing, returned field name, and `(documentId, chunkId)` pair was observed in actual tool results during this execution. Host validation does **not** perform natural-language semantic entailment or verify the factual truth of free-text `Answer` or `Claim` strings against database values. Consumers must treat `Answer` and `Claim` text as model-authored synthesis backed by verified source pointers.

```
Stage 1: Tool-Selection Loop (Tools = [.. session.Tools], ResponseFormat = null)
   │
   ├── Pre-call: EvidenceLedger.TryValidatePaginationRequest (page==1 first, page==prior+1, stable pageSize)
   ├── Tool runs: Task.WhenAny + PerCallTimeout (20s) + OverallTimeout (60s)
   ├── Fail-closed indexing: EvidenceLedger.TryRecordToolOutput (unwrap MCP CallToolResult, verify page math)
   └── Model signals completion (0 function calls, non-empty candidate text)
   │
   ▼
Stage 2: Structured Output & Repair Loop (Tools = null, ResponseFormat = JsonSchema<AgentModelDraft>)
   │
   ├── Appends formatting prompt (User)
   ├── Loops 0..MaxValidationRetries (2), bounded by MaxModelTurns (6), PerCallTimeout (20s), OverallTimeout (60s)
   ├── AgentOutputValidator.Validate (<= 32,768 UTF-8 bytes, duplicate-key scan, strict JSON, provenance, disclosure)
   │     ├── Pass? ──> Host injects toolsUsed + normalizes truncation disclosure -> AgentQueryResponse ("1.0")
   │     └── Fail? ──> Appends invalid draft (Assistant) + repair prompt (User), retries if budget permits
   │                     └── Exhausted / Turn limit / Timeout? ──> AgentQueryResponse.Failure(AgentErrorCode, ...)
   ▼
Public Response (AgentQueryResponse)
```

### In Scope for Phase 3
1. **Constrained Failure & Contract Upgrade (`Contracts/AgentContracts.cs`):**
   - Closed `AgentErrorCode` enum and fixed `AgentErrorCatalog` public messages so internal exceptions or diagnostic strings never leak to API callers.
   - Strict separation between model-generated `AgentModelDraft` (`[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]`) and host-assembled public `AgentQueryResponse` (`SchemaVersion = "1.0"`, `IReadOnlyList<ToolExecutionRecord> ToolsUsed`).
   - Explicit upgrade of Phase 2's `AgentQueryResponse`, `AgentRunResult`, and `Program.cs` endpoint mapping to the versioned Phase 3 contract (`ADR-007`, [Design Log §6](MCP-Agent-Design-Log.md#L178-L213)).
2. **Per-Run `EvidenceLedger` (`Orchestration/EvidenceLedger.cs`):**
   - Fail-closed `TryRecordToolOutput` unwrapping real `ModelContextProtocol` `1.3.0` `CallToolResult` payloads (`structuredContent`, `isError`, `content` text JSON) as well as typed DTOs/stub JSON, halting with `EVIDENCE_INDEXING_ERROR` if a catalogue payload cannot be parsed or fails structural invariants.
   - Pagination sequence & metadata validation (`LastPage`, `PageSize`, `TotalCount`, `HasMore`) rejecting out-of-order first pages, gaps, mid-query page-size changes, calls after `hasMore == false`, or inconsistent math.
   - Query-scoped empty-search authorization (`CanAuthorizeZeroCitationAnswer`) requiring exactly one distinct empty search, zero indexed books/chunks across the run, and active filter terms echoed in `Answer`.
   - Linked `(bookId, editionId?) -> ReturnedFields` indexing and Phase 4-ready `(documentId, chunkId) -> Excerpt` hooks.
3. **Strict `AgentOutputValidator` (`Validation/AgentOutputValidator.cs`):**
   - Post-materialization `32,768`-byte validation and history guard.
   - `Utf8JsonReader` duplicate-property scan + `JsonUnmappedMemberHandling.Disallow` deserialization.
   - Linked provenance verification, scoped empty-search checks, and checkable pagination truncation disclosure rules.
4. **Two-Stage `AgentLoopRunner` (`Orchestration/AgentLoopRunner.cs`):**
   - Reserves Stage 2 turn budget (`MaxModelTurns - 1` max turns in Stage 1), enforces `PerCallTimeout` on every Stage 2 call, enforces alternating `User -> Assistant -> User` repair conversation ordering, and emits Stage 2 OpenTelemetry tags (`agent.stage`, `agent.validation_attempt`, `agent.validation_passed`, `agent.validation_retries_used`) with `EnableSensitiveData = false`.
5. **Deterministic Unit Test Suite (`tests/PublishingTracker.Agent.Tests/Phase3EvidenceAndValidationTests.cs`):** 22 deterministic xUnit scenarios using `ScriptedChatClient`.

### Out of Scope for Phase 3
- **`pgvector` RAG Database Storage & `search_publishing_guidelines` Tool** (Phase 4 — `EvidenceLedger` and `AgentOutputValidator` define document evidence methods now so Phase 4 integrates without contract changes).
- **Free-Text Semantic Entailment Checking** (As noted above and in `ADR-007`).
- **Live OpenRouter/Claude Smoke Tests & Containerization** (Phases 5 and 6).

---

## 2. Contracts, Error Taxonomy, & Provenance Boundaries (`Contracts/AgentContracts.cs`)

Phase 3 evolves Phase 2's flat string-based `AgentQueryResponse` and `AgentRunResult` (`src/PublishingTracker.Agent/Contracts/AgentQueryResponse.cs`, `Orchestration/AgentRunResult.cs`, and `Program.cs`) to match the locked `1.0` Agent Host Response Schema in [MCP-Agent-Design-Log.md §6](MCP-Agent-Design-Log.md#L178-L213).

### 2.1 Closed Error Taxonomy (`AgentErrorCode` & `AgentErrorCatalog`)
To guarantee internal exception details, stack fragments, or host paths are never exposed in HTTP responses, `AgentQueryResponse.Failure` accepts only an `AgentErrorCode` enum value. Diagnostic details are written strictly to `ILogger`.

```csharp
namespace PublishingTracker.Agent.Contracts;

public enum AgentErrorCode
{
    CANCELLED,
    TIMEOUT,
    TOOL_DISCOVERY_ERROR,
    INVALID_TOOLSET,
    MODEL_ERROR,
    EMPTY_RESPONSE,
    TURN_LIMIT_EXCEEDED,
    BUDGET_EXCEEDED,
    UNKNOWN_TOOL,
    INVALID_TOOL_ARGUMENTS,
    REPEAT_CALL_LIMIT,
    PAGINATION_LIMIT_EXCEEDED,
    PAGINATION_SEQUENCE_ERROR,
    TOOL_ERROR,
    SERIALIZATION_ERROR,
    PAYLOAD_LIMIT_EXCEEDED,
    EVIDENCE_INDEXING_ERROR,
    VALIDATION_RETRIES_EXHAUSTED
}

internal static class AgentErrorCatalog
{
    private static readonly IReadOnlyDictionary<AgentErrorCode, string> PublicMessages =
        new Dictionary<AgentErrorCode, string>
        {
            [AgentErrorCode.CANCELLED] = "CANCELLED: The request was cancelled by the caller.",
            [AgentErrorCode.TIMEOUT] = "TIMEOUT: The agent request exceeded a per-call or overall timeout.",
            [AgentErrorCode.TOOL_DISCOVERY_ERROR] = "TOOL_DISCOVERY_ERROR: MCP tools could not be loaded.",
            [AgentErrorCode.INVALID_TOOLSET] = "INVALID_TOOLSET: The MCP server returned an invalid or duplicate toolset.",
            [AgentErrorCode.MODEL_ERROR] = "MODEL_ERROR: The model call failed.",
            [AgentErrorCode.EMPTY_RESPONSE] = "EMPTY_RESPONSE: The model returned an empty response.",
            [AgentErrorCode.TURN_LIMIT_EXCEEDED] = "TURN_LIMIT_EXCEEDED: The maximum model-turn budget was reached.",
            [AgentErrorCode.BUDGET_EXCEEDED] = "BUDGET_EXCEEDED: The maximum tool-call budget was exceeded.",
            [AgentErrorCode.UNKNOWN_TOOL] = "UNKNOWN_TOOL: The model requested an unavailable tool.",
            [AgentErrorCode.INVALID_TOOL_ARGUMENTS] = "INVALID_TOOL_ARGUMENTS: The requested tool arguments were invalid.",
            [AgentErrorCode.REPEAT_CALL_LIMIT] = "REPEAT_CALL_LIMIT: An identical tool call exceeded the repeat limit.",
            [AgentErrorCode.PAGINATION_LIMIT_EXCEEDED] = "PAGINATION_LIMIT_EXCEEDED: The requested page exceeded the maximum page budget.",
            [AgentErrorCode.PAGINATION_SEQUENCE_ERROR] = "PAGINATION_SEQUENCE_ERROR: Pagination parameters violated sequential paging rules.",
            [AgentErrorCode.TOOL_ERROR] = "TOOL_ERROR: A catalogue tool encountered an execution error.",
            [AgentErrorCode.SERIALIZATION_ERROR] = "SERIALIZATION_ERROR: A tool result could not be serialized.",
            [AgentErrorCode.PAYLOAD_LIMIT_EXCEEDED] = "PAYLOAD_LIMIT_EXCEEDED: A tool result exceeded the maximum allowed payload size.",
            [AgentErrorCode.EVIDENCE_INDEXING_ERROR] = "EVIDENCE_INDEXING_ERROR: A tool result did not match the expected evidence schema or paging invariants.",
            [AgentErrorCode.VALIDATION_RETRIES_EXHAUSTED] = "VALIDATION_RETRIES_EXHAUSTED: Output failed schema or provenance validation after maximum retries."
        };

    public static string GetPublicMessage(AgentErrorCode code) =>
        PublicMessages.TryGetValue(code, out var message)
            ? message
            : "MODEL_ERROR: An unexpected orchestration error occurred.";
}
```

### 2.2 Model-Generated Draft (`AgentModelDraft`) vs. Host Envelope (`AgentQueryResponse`)
Every model-draft record uses `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]` so unexpected fields (including a model attempting to fabricate `"toolsUsed"` or `"schemaVersion"`) fail strict deserialization during validation.

```csharp
using System.Text.Json.Serialization;

namespace PublishingTracker.Agent.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CatalogueSourceCitation(
    int BookId,
    int? EditionId,
    IReadOnlyList<string> Fields,
    /// <summary>Model-authored summary claim; validated only for source/field provenance, not semantic entailment.</summary>
    string Claim);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DocumentSourceCitation(
    string DocumentId,
    string ChunkId,
    /// <summary>Model-authored summary claim; validated only for document/chunk provenance, not semantic entailment.</summary>
    string Claim);

/// <summary>Structured provenance pointers verified against the per-run EvidenceLedger (does not imply free-text semantic verification).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentEvidenceDraft(
    IReadOnlyList<CatalogueSourceCitation> CatalogueSources,
    IReadOnlyList<DocumentSourceCitation> DocumentSources);

/// <summary>Structured draft requested from IChatClient in Stage 2 via ChatResponseFormat.ForJsonSchema&lt;AgentModelDraft&gt;().</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentModelDraft(
    string Status,
    string Answer,
    AgentEvidenceDraft Evidence,
    string? Limitations);

public sealed record ToolExecutionRecord(
    string ToolName,
    int Turn,
    long DurationMs,
    bool Succeeded);

public sealed record AgentQueryResponse(
    string SchemaVersion,
    string Status,
    string? Answer,
    AgentEvidenceDraft Evidence,
    IReadOnlyList<ToolExecutionRecord> ToolsUsed,
    int Turns,
    int TotalToolCalls,
    string? Limitations,
    string? Error)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string TruncationDisclosureNote =
        "Catalogue results were truncated at the page limit (hasMore=true); additional matching titles exist.";

    public static AgentQueryResponse Answered(
        AgentModelDraft draft,
        bool hasPaginationTruncation,
        IReadOnlyList<ToolExecutionRecord> toolsUsed,
        int turns,
        int totalToolCalls)
    {
        var limitations = draft.Limitations?.Trim();
        if (hasPaginationTruncation &&
            (string.IsNullOrWhiteSpace(limitations) ||
             !limitations.Contains("hasMore=true", StringComparison.OrdinalIgnoreCase)))
        {
            limitations = string.IsNullOrWhiteSpace(limitations)
                ? TruncationDisclosureNote
                : $"{limitations} ({TruncationDisclosureNote})";
        }

        return new AgentQueryResponse(
            SchemaVersion: CurrentSchemaVersion,
            Status: draft.Status,
            Answer: draft.Answer,
            Evidence: draft.Evidence,
            ToolsUsed: toolsUsed,
            Turns: turns,
            TotalToolCalls: totalToolCalls,
            Limitations: string.IsNullOrWhiteSpace(limitations) ? null : limitations,
            Error: null);
    }

    public static AgentQueryResponse Failure(
        AgentErrorCode errorCode,
        IReadOnlyList<ToolExecutionRecord> toolsUsed,
        int turns,
        int totalToolCalls,
        bool hasPaginationTruncation = false) =>
        new(
            SchemaVersion: CurrentSchemaVersion,
            Status: "could_not_answer",
            Answer: null,
            Evidence: new AgentEvidenceDraft([], []),
            ToolsUsed: toolsUsed,
            Turns: turns,
            TotalToolCalls: totalToolCalls,
            Limitations: hasPaginationTruncation ? TruncationDisclosureNote : null,
            Error: AgentErrorCatalog.GetPublicMessage(errorCode));
}
```

---

## 3. Workstream 1 — Fail-Closed `EvidenceLedger` (`Orchestration/EvidenceLedger.cs`)

### 3.1 Provenance & State Models
- **`CatalogueEvidenceKey(int BookId, int? EditionId)`** $\to$ `HashSet<string>` (`StringComparer.OrdinalIgnoreCase`)
- **`DocumentEvidenceKey(string DocumentId, string ChunkId)`** $\to$ `string Excerpt`
- **`PaginationQueryState(int LastPage, int PageSize, int TotalCount, bool HasMore)`** keyed by normalized filter signature (`toolName|format=...|missingField=...`).
- **`CatalogueSearchRecord(string? Format, string? MissingField, int TotalCount, int ItemsReturned)`** keyed by the same normalized filter signature so empty-search exceptions are bound strictly to their specific query filter.

### 3.2 Real MCP Result Shape & Fail-Closed Extraction
In `ModelContextProtocol` `1.3.0`, invoking `McpClientTool.InvokeAsync(AIFunctionArguments, ct)` returns a `System.Text.Json.JsonElement` containing the serialized `ModelContextProtocol.Protocol.CallToolResult`:

```json
{
  "isError": false,
  "structuredContent": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 1,
    "hasMore": false,
    "items": [ ... ]
  },
  "content": [
    { "type": "text", "text": "{...}" }
  ]
}
```

Meanwhile, in-process stub `AIFunction` tools in tests may return typed `MissingMetadataPageDto` / `TitleDetailsDto` instances or raw JSON `JsonElement` objects directly. `EvidenceLedger.TryRecordToolOutput` handles all three deterministically and **fails closed** (`AgentErrorCode.EVIDENCE_INDEXING_ERROR` or `AgentErrorCode.TOOL_ERROR`) whenever:
- A `CallToolResult` envelope reports `"isError": true` $\to$ `AgentErrorCode.TOOL_ERROR`.
- Unwrapping `structuredContent` (or fallback single `content[0].text` JSON block) fails or required schema properties are absent/wrong-typed $\to$ `AgentErrorCode.EVIDENCE_INDEXING_ERROR`.
- Any book or edition DTO invariant fails (`BookId <= 0`, `Title` blank, `MissingBookFields` null, `Editions` null, `EditionId <= 0`, `Format` blank, `MissingFields` null, or for `get_title_details`: `Author` blank or `PlatformSummaries` null) $\to$ `AgentErrorCode.EVIDENCE_INDEXING_ERROR`.

### 3.3 Sequential Pagination & Metadata Consistency Rules
1. **Pre-Call Validation (`TryValidatePaginationRequest`)** before calling `find_titles_missing_metadata`:
   - Extract `page` (default `1`) and `pageSize` (default `20`). Reject non-integer `page`/`pageSize` arguments or `pageSize < 1 || pageSize > 50` with `AgentErrorCode.INVALID_TOOL_ARGUMENTS`.
   - Reject `page < 1 || page > maxPages` or cumulative catalogue pages fetched $\ge$ `maxPages` (`2`) with `AgentErrorCode.PAGINATION_LIMIT_EXCEEDED`.
   - When no prior page exists for `filterKey`: require `page == 1` (reject starting at `page: 2` with `AgentErrorCode.PAGINATION_SEQUENCE_ERROR`).
   - When a prior `PaginationQueryState` exists for `filterKey`:
     - Require `page == prior.LastPage + 1` (no gaps or duplicate pages).
     - Require `pageSize == prior.PageSize` (reject mid-query `pageSize` changes with `AgentErrorCode.PAGINATION_SEQUENCE_ERROR`).
     - Require `prior.HasMore == true` (reject calling page 2 after page 1 already reported `hasMore: false` with `AgentErrorCode.PAGINATION_SEQUENCE_ERROR`).
2. **Post-Call Validation (inside `TryRecordToolOutput`)**:
   - Require `dto.Page == requestedPage` and `dto.PageSize == requestedPageSize`.
   - Require `dto.TotalCount >= 0` and `dto.Items.Count <= dto.PageSize`.
   - If a prior page exists for `filterKey`, require `dto.TotalCount == prior.TotalCount`.
   - Verify exact paging boundary arithmetic:
     - If `dto.TotalCount == 0`, require `dto.Items.Count == 0 && !dto.HasMore`.
     - If `!dto.HasMore`, require `((dto.Page - 1) * dto.PageSize) + dto.Items.Count == dto.TotalCount`.
     - If `dto.HasMore`, require `dto.Items.Count > 0 && ((dto.Page - 1) * dto.PageSize) + dto.Items.Count < dto.TotalCount`.
   - On any inconsistency, reject immediately with `AgentErrorCode.EVIDENCE_INDEXING_ERROR`.

### 3.4 Scoped Empty-Search Exception (`CanAuthorizeZeroCitationAnswer`)
A zero-citation `"answered"` draft cannot be justified globally from an unrelated empty query earlier in the run. `CanAuthorizeZeroCitationAnswer(string answerText, out string error)` returns `true` **only when all four checks pass**:
1. Exactly **one** distinct `find_titles_missing_metadata` filter query was executed (`_searchesByFilter.Count == 1`).
2. That query returned `TotalCount == 0 && ItemsReturned == 0`.
3. Zero catalogue items (`_catalogue.Count == 0`) and zero document chunks (`_documents.Count == 0`) were indexed anywhere in the run.
4. Every active filter present on that empty query (`Format` if non-whitespace, `MissingField` if non-whitespace) appears in `answerText` (case-insensitive comparison).

### 3.5 Implementation Sketch (`EvidenceLedger.cs`)

```csharp
using System.Globalization;
using System.Text.Json;
using PublishingTracker.Agent.Contracts;
using PublishingTracker.Application.Books.Queries;

namespace PublishingTracker.Agent.Orchestration;

public readonly record struct CatalogueEvidenceKey(int BookId, int? EditionId);
public readonly record struct DocumentEvidenceKey(string DocumentId, string ChunkId);
public sealed record PaginationQueryState(int LastPage, int PageSize, int TotalCount, bool HasMore);
public sealed record CatalogueSearchRecord(string? Format, string? MissingField, int TotalCount, int ItemsReturned);

public sealed class EvidenceLedger
{
    private const int DefaultPageSize = 20;
    private const int MaxAllowedPageSize = 50;

    private static readonly string[] MissingMetadataBookFields =
        ["BookId", "Title", "MissingBookFields", "Editions"];

    private static readonly string[] MissingMetadataEditionFields =
        ["EditionId", "Format", "MissingFields"];

    private static readonly string[] TitleDetailsBookFields =
    [
        "BookId", "Title", "Author", "SeriesName", "SeriesNumber", "Genre",
        "Description", "CoverImageUrl", "CreatedAt", "UpdatedAt", "MissingBookFields", "Editions"
    ];

    private static readonly string[] TitleDetailsEditionFields =
    [
        "EditionId", "Format", "ISBN", "PublicationDate", "BasePrice", "PageCount",
        "Dimensions", "FileSize", "Narrator", "Duration", "MissingFields",
        "PlatformSummaries", "PlatformId", "PlatformName", "IsPublished", "PublishedDate", "PlatformProductId"
    ];

    private readonly Dictionary<CatalogueEvidenceKey, HashSet<string>> _catalogue = new();
    private readonly Dictionary<DocumentEvidenceKey, string> _documents = new();
    private readonly Dictionary<string, PaginationQueryState> _paginationByFilter = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CatalogueSearchRecord> _searchesByFilter = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ToolExecutionRecord> _toolsUsed = new();

    private int _totalCataloguePagesFetched;

    public IReadOnlyList<ToolExecutionRecord> ToolsUsed => _toolsUsed;
    public bool HasPaginationTruncation => _paginationByFilter.Values.Any(state => state.HasMore);

    public void RecordToolExecution(string toolName, int turn, long durationMs, bool succeeded) =>
        _toolsUsed.Add(new ToolExecutionRecord(toolName, turn, durationMs, succeeded));

    public bool TryValidatePaginationRequest(
        string toolName,
        IDictionary<string, object?>? arguments,
        int maxPaginationPages,
        out AgentErrorCode errorCode,
        out string diagnosticError)
    {
        if (!string.Equals(toolName, "find_titles_missing_metadata", StringComparison.Ordinal))
        {
            errorCode = default;
            diagnosticError = string.Empty;
            return true;
        }

        if (!TryExtractPositiveOrDefaultInt(arguments, "page", defaultValue: 1, out var page))
        {
            errorCode = AgentErrorCode.INVALID_TOOL_ARGUMENTS;
            diagnosticError = "Tool argument 'page' must be a valid integer.";
            return false;
        }

        if (!TryExtractPositiveOrDefaultInt(arguments, "pageSize", defaultValue: DefaultPageSize, out var pageSize) ||
            pageSize < 1 || pageSize > MaxAllowedPageSize)
        {
            errorCode = AgentErrorCode.INVALID_TOOL_ARGUMENTS;
            diagnosticError = $"Tool argument 'pageSize' must be between 1 and {MaxAllowedPageSize}.";
            return false;
        }

        if (page < 1 || page > maxPaginationPages || _totalCataloguePagesFetched >= maxPaginationPages)
        {
            errorCode = AgentErrorCode.PAGINATION_LIMIT_EXCEEDED;
            diagnosticError = $"Pagination request page={page} exceeds maximum allowed page budget of {maxPaginationPages}.";
            return false;
        }

        var (filterKey, _, _) = BuildMissingMetadataFilterKey(arguments);
        if (!_paginationByFilter.TryGetValue(filterKey, out var prior))
        {
            if (page != 1)
            {
                errorCode = AgentErrorCode.PAGINATION_SEQUENCE_ERROR;
                diagnosticError = $"First request for filter '{filterKey}' must start at page 1, got page {page}.";
                return false;
            }
        }
        else
        {
            if (!prior.HasMore)
            {
                errorCode = AgentErrorCode.PAGINATION_SEQUENCE_ERROR;
                diagnosticError = $"Filter '{filterKey}' already reached hasMore=false on page {prior.LastPage}.";
                return false;
            }

            if (page != prior.LastPage + 1)
            {
                errorCode = AgentErrorCode.PAGINATION_SEQUENCE_ERROR;
                diagnosticError = $"Expected sequential page {prior.LastPage + 1} for filter '{filterKey}', got page {page}.";
                return false;
            }

            if (pageSize != prior.PageSize)
            {
                errorCode = AgentErrorCode.PAGINATION_SEQUENCE_ERROR;
                diagnosticError = $"PageSize changed mid-query from {prior.PageSize} to {pageSize} for filter '{filterKey}'.";
                return false;
            }
        }

        errorCode = default;
        diagnosticError = string.Empty;
        return true;
    }

    public bool TryRecordToolOutput(
        string toolName,
        IDictionary<string, object?>? arguments,
        object? rawResult,
        string serializedJson,
        out AgentErrorCode errorCode,
        out string diagnosticError)
    {
        if (string.Equals(toolName, "find_titles_missing_metadata", StringComparison.Ordinal))
        {
            if (!TryUnwrapMissingMetadataPage(rawResult, serializedJson, out var pageDto, out errorCode, out diagnosticError) || pageDto is null)
            {
                return false;
            }

            return TryIndexMissingMetadataPage(arguments, pageDto, out errorCode, out diagnosticError);
        }

        if (string.Equals(toolName, "get_title_details", StringComparison.Ordinal))
        {
            if (!TryUnwrapTitleDetails(rawResult, serializedJson, out var detailsDto, out errorCode, out diagnosticError) || detailsDto is null)
            {
                return false;
            }

            return TryIndexTitleDetails(detailsDto, out errorCode, out diagnosticError);
        }

        errorCode = default;
        diagnosticError = string.Empty;
        return true;
    }

    public void RecordDocumentHit(string documentId, string chunkId, string excerpt)
    {
        if (string.IsNullOrWhiteSpace(documentId) || string.IsNullOrWhiteSpace(chunkId))
            return;

        _documents[new DocumentEvidenceKey(documentId, chunkId)] = excerpt;
    }

    public bool CanAuthorizeZeroCitationAnswer(string answerText, out string error)
    {
        if (_searchesByFilter.Count != 1)
        {
            error = "Status 'answered' requires at least one corroborated evidence entry (zero-citation answers are permitted only for a single empty catalogue search).";
            return false;
        }

        if (_catalogue.Count > 0 || _documents.Count > 0)
        {
            error = "Status 'answered' requires citations whenever catalogue titles or documents were returned in the run.";
            return false;
        }

        var onlySearch = _searchesByFilter.Values.Single();
        if (onlySearch.TotalCount != 0 || onlySearch.ItemsReturned != 0)
        {
            error = "Status 'answered' requires at least one corroborated evidence entry.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(onlySearch.Format) &&
            !answerText.Contains(onlySearch.Format, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Uncited empty-search answer must reference the queried format filter '{onlySearch.Format}'.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(onlySearch.MissingField) &&
            !answerText.Contains(onlySearch.MissingField, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Uncited empty-search answer must reference the queried missingField filter '{onlySearch.MissingField}'.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryValidateCatalogueCitation(
        int bookId,
        int? editionId,
        IReadOnlyList<string> fields,
        out string error)
    {
        if (editionId is null)
        {
            if (!_catalogue.TryGetValue(new CatalogueEvidenceKey(bookId, null), out var bookFields))
            {
                error = $"Uncorroborated catalogue link: BookId={bookId} was not returned in any tool result.";
                return false;
            }

            return VerifyFieldsExist(bookId, null, fields, bookFields, out error);
        }

        if (!_catalogue.TryGetValue(new CatalogueEvidenceKey(bookId, editionId.Value), out var editionFields))
        {
            error = $"Uncorroborated catalogue link: BookId={bookId} with EditionId={editionId.Value} was not returned together in this run.";
            return false;
        }

        var combinedFields = new HashSet<string>(editionFields, StringComparer.OrdinalIgnoreCase);
        if (_catalogue.TryGetValue(new CatalogueEvidenceKey(bookId, null), out var parentBookFields))
        {
            combinedFields.UnionWith(parentBookFields);
        }

        return VerifyFieldsExist(bookId, editionId, fields, combinedFields, out error);
    }

    public bool HasDocumentEvidence(string documentId, string chunkId) =>
        _documents.ContainsKey(new DocumentEvidenceKey(documentId, chunkId));

    private bool TryIndexMissingMetadataPage(
        IDictionary<string, object?>? arguments,
        MissingMetadataPageDto page,
        out AgentErrorCode errorCode,
        out string diagnosticError)
    {
        TryExtractPositiveOrDefaultInt(arguments, "page", defaultValue: 1, out var requestedPage);
        TryExtractPositiveOrDefaultInt(arguments, "pageSize", defaultValue: DefaultPageSize, out var requestedPageSize);

        if (page.Page != requestedPage || page.PageSize != requestedPageSize || page.TotalCount < 0 || page.Items is null)
        {
            errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
            diagnosticError = $"Returned pagination metadata (page={page.Page}, pageSize={page.PageSize}, totalCount={page.TotalCount}) does not match request (page={requestedPage}, pageSize={requestedPageSize}).";
            return false;
        }

        if (page.Items.Count > page.PageSize)
        {
            errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
            diagnosticError = $"Returned item count {page.Items.Count} exceeds pageSize {page.PageSize}.";
            return false;
        }

        var (filterKey, format, missingField) = BuildMissingMetadataFilterKey(arguments);
        if (_paginationByFilter.TryGetValue(filterKey, out var prior) && page.TotalCount != prior.TotalCount)
        {
            errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
            diagnosticError = $"TotalCount changed mid-pagination from {prior.TotalCount} to {page.TotalCount} for '{filterKey}'.";
            return false;
        }

        var itemsSeenSoFar = ((page.Page - 1) * page.PageSize) + page.Items.Count;
        var countsConsistent = page.TotalCount == 0
            ? page.Items.Count == 0 && !page.HasMore
            : page.HasMore
                ? page.Items.Count > 0 && itemsSeenSoFar < page.TotalCount
                : itemsSeenSoFar == page.TotalCount;

        if (!countsConsistent)
        {
            errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
            diagnosticError = $"Inconsistent pagination totals: page={page.Page}, pageSize={page.PageSize}, count={page.Items.Count}, totalCount={page.TotalCount}, hasMore={page.HasMore}.";
            return false;
        }

        foreach (var item in page.Items)
        {
            if (item is null || item.BookId <= 0 || string.IsNullOrWhiteSpace(item.Title) ||
                item.MissingBookFields is null || item.Editions is null)
            {
                errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
                diagnosticError = "Catalogue page contains invalid item metadata.";
                return false;
            }

            AddCatalogueFields(item.BookId, null, MissingMetadataBookFields);
            AddCatalogueFields(item.BookId, null, item.MissingBookFields);

            foreach (var edition in item.Editions)
            {
                if (edition is null || edition.EditionId <= 0 || string.IsNullOrWhiteSpace(edition.Format) ||
                    edition.MissingFields is null)
                {
                    errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
                    diagnosticError = $"BookId={item.BookId} contains malformed edition entry.";
                    return false;
                }

                AddCatalogueFields(item.BookId, edition.EditionId, MissingMetadataEditionFields);
                AddCatalogueFields(item.BookId, edition.EditionId, edition.MissingFields);
            }
        }

        _totalCataloguePagesFetched++;
        _paginationByFilter[filterKey] = new PaginationQueryState(page.Page, page.PageSize, page.TotalCount, page.HasMore);
        _searchesByFilter[filterKey] = new CatalogueSearchRecord(format, missingField, page.TotalCount, page.Items.Count);

        errorCode = default;
        diagnosticError = string.Empty;
        return true;
    }

    private bool TryIndexTitleDetails(
        TitleDetailsDto details,
        out AgentErrorCode errorCode,
        out string diagnosticError)
    {
        if (details.BookId <= 0 || string.IsNullOrWhiteSpace(details.Title) || string.IsNullOrWhiteSpace(details.Author) ||
            details.MissingBookFields is null || details.Editions is null)
        {
            errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
            diagnosticError = "TitleDetailsDto is missing required BookId, Title, Author, MissingBookFields, or Editions.";
            return false;
        }

        AddCatalogueFields(details.BookId, null, TitleDetailsBookFields);
        AddCatalogueFields(details.BookId, null, details.MissingBookFields);

        foreach (var edition in details.Editions)
        {
            if (edition is null || edition.EditionId <= 0 || string.IsNullOrWhiteSpace(edition.Format) ||
                edition.MissingFields is null || edition.PlatformSummaries is null)
            {
                errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
                diagnosticError = $"TitleDetailsDto for BookId={details.BookId} contains malformed edition metadata.";
                return false;
            }

            AddCatalogueFields(details.BookId, edition.EditionId, TitleDetailsEditionFields);
            AddCatalogueFields(details.BookId, edition.EditionId, edition.MissingFields);
        }

        errorCode = default;
        diagnosticError = string.Empty;
        return true;
    }

    private static bool TryUnwrapMissingMetadataPage(
        object? rawResult,
        string serializedJson,
        out MissingMetadataPageDto? dto,
        out AgentErrorCode errorCode,
        out string diagnosticError)
    {
        if (rawResult is MissingMetadataPageDto typed)
        {
            dto = typed;
            errorCode = default;
            diagnosticError = string.Empty;
            return true;
        }

        if (!TryExtractPayloadElement(serializedJson, out var payloadElement, out errorCode, out diagnosticError))
        {
            dto = null;
            return false;
        }

        if (payloadElement.ValueKind != JsonValueKind.Object ||
            !payloadElement.TryGetProperty("page", out _) ||
            !payloadElement.TryGetProperty("pageSize", out _) ||
            !payloadElement.TryGetProperty("totalCount", out _) ||
            !payloadElement.TryGetProperty("hasMore", out _) ||
            !payloadElement.TryGetProperty("items", out var itemsProp) ||
            itemsProp.ValueKind != JsonValueKind.Array)
        {
            dto = null;
            errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
            diagnosticError = "Unwrapped find_titles_missing_metadata payload is missing required page/pageSize/totalCount/hasMore/items fields.";
            return false;
        }

        try
        {
            dto = payloadElement.Deserialize<MissingMetadataPageDto>(JsonSerializerOptions.Web);
            if (dto is null)
            {
                errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
                diagnosticError = "Deserialized MissingMetadataPageDto was null.";
                return false;
            }

            errorCode = default;
            diagnosticError = string.Empty;
            return true;
        }
        catch (JsonException ex)
        {
            dto = null;
            errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
            diagnosticError = $"Failed to deserialize MissingMetadataPageDto: {ex.Message}";
            return false;
        }
    }

    private static bool TryUnwrapTitleDetails(
        object? rawResult,
        string serializedJson,
        out TitleDetailsDto? dto,
        out AgentErrorCode errorCode,
        out string diagnosticError)
    {
        if (rawResult is TitleDetailsDto typed)
        {
            dto = typed;
            errorCode = default;
            diagnosticError = string.Empty;
            return true;
        }

        if (!TryExtractPayloadElement(serializedJson, out var payloadElement, out errorCode, out diagnosticError))
        {
            dto = null;
            return false;
        }

        if (payloadElement.ValueKind != JsonValueKind.Object ||
            !payloadElement.TryGetProperty("bookId", out _) ||
            !payloadElement.TryGetProperty("title", out _) ||
            !payloadElement.TryGetProperty("author", out _) ||
            !payloadElement.TryGetProperty("missingBookFields", out _) ||
            !payloadElement.TryGetProperty("editions", out var editionsProp) ||
            editionsProp.ValueKind != JsonValueKind.Array)
        {
            dto = null;
            errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
            diagnosticError = "Unwrapped get_title_details payload is missing required bookId/title/author/missingBookFields/editions fields.";
            return false;
        }

        try
        {
            dto = payloadElement.Deserialize<TitleDetailsDto>(JsonSerializerOptions.Web);
            if (dto is null)
            {
                errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
                diagnosticError = "Deserialized TitleDetailsDto was null.";
                return false;
            }

            errorCode = default;
            diagnosticError = string.Empty;
            return true;
        }
        catch (JsonException ex)
        {
            dto = null;
            errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
            diagnosticError = $"Failed to deserialize TitleDetailsDto: {ex.Message}";
            return false;
        }
    }

    private static bool TryExtractPayloadElement(
        string serializedJson,
        out JsonElement payloadElement,
        out AgentErrorCode errorCode,
        out string diagnosticError)
    {
        try
        {
            using var doc = JsonDocument.Parse(serializedJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                payloadElement = default;
                errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
                diagnosticError = "Tool result JSON root is not an object.";
                return false;
            }

            // Detect ModelContextProtocol CallToolResult envelope
            if (root.TryGetProperty("isError", out var isErrorProp) && isErrorProp.ValueKind == JsonValueKind.True)
            {
                payloadElement = default;
                errorCode = AgentErrorCode.TOOL_ERROR;
                diagnosticError = "MCP CallToolResult indicated isError=true.";
                return false;
            }

            if (root.TryGetProperty("structuredContent", out var structuredProp) &&
                structuredProp.ValueKind == JsonValueKind.Object)
            {
                payloadElement = structuredProp.Clone();
                errorCode = default;
                diagnosticError = string.Empty;
                return true;
            }

            if (root.TryGetProperty("content", out var contentProp) &&
                contentProp.ValueKind == JsonValueKind.Array &&
                contentProp.GetArrayLength() > 0)
            {
                var first = contentProp[0];
                if (first.TryGetProperty("text", out var textProp) &&
                    textProp.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(textProp.GetString()))
                {
                    using var innerDoc = JsonDocument.Parse(textProp.GetString()!);
                    payloadElement = innerDoc.RootElement.Clone();
                    errorCode = default;
                    diagnosticError = string.Empty;
                    return true;
                }
            }

            // Direct DTO JSON shape (used by stub AIFunctions in tests)
            if (!root.TryGetProperty("content", out _))
            {
                payloadElement = root.Clone();
                errorCode = default;
                diagnosticError = string.Empty;
                return true;
            }

            payloadElement = default;
            errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
            diagnosticError = "CallToolResult contained neither structuredContent nor parseable text JSON.";
            return false;
        }
        catch (JsonException ex)
        {
            payloadElement = default;
            errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
            diagnosticError = $"Failed to parse tool JSON envelope: {ex.Message}";
            return false;
        }
    }

    private void AddCatalogueFields(int bookId, int? editionId, IEnumerable<string> fields)
    {
        var key = new CatalogueEvidenceKey(bookId, editionId);
        if (!_catalogue.TryGetValue(key, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _catalogue[key] = set;
        }

        foreach (var field in fields)
        {
            if (!string.IsNullOrWhiteSpace(field))
                set.Add(field.Trim());
        }
    }

    private static bool VerifyFieldsExist(
        int bookId,
        int? editionId,
        IReadOnlyList<string> requestedFields,
        HashSet<string> allowedFields,
        out string error)
    {
        foreach (var field in requestedFields)
        {
            if (!allowedFields.Contains(field))
            {
                var scope = editionId is int eid ? $"BookId={bookId}, EditionId={eid}" : $"BookId={bookId}";
                error = $"Unreturned field '{field}' cited for {scope}.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static (string Key, string? Format, string? MissingField) BuildMissingMetadataFilterKey(
        IDictionary<string, object?>? arguments)
    {
        string? format = null;
        string? missingField = null;

        if (arguments is not null)
        {
            if (arguments.TryGetValue("format", out var fRaw) && !string.IsNullOrWhiteSpace(fRaw?.ToString()))
                format = fRaw!.ToString()!.Trim();

            if (arguments.TryGetValue("missingField", out var mRaw) && !string.IsNullOrWhiteSpace(mRaw?.ToString()))
                missingField = mRaw!.ToString()!.Trim();
        }

        return ($"find_titles_missing_metadata|format={format ?? ""}|missingField={missingField ?? ""}", format, missingField);
    }

    private static bool TryExtractPositiveOrDefaultInt(
        IDictionary<string, object?>? arguments,
        string key,
        int defaultValue,
        out int value)
    {
        value = defaultValue;
        if (arguments is null || !arguments.TryGetValue(key, out var raw) || raw is null)
            return true;

        if (raw is int i)
        {
            value = i;
            return true;
        }

        if (raw is JsonElement elem && elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out var parsedElem))
        {
            value = parsedElem;
            return true;
        }

        return int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
```

---

## 4. Workstream 2 — Strict Local Validator (`Validation/AgentOutputValidator.cs`)

### Guard & Deserialization Clarifications
- **Post-Materialization Guard (Not an Allocation Limit):** The `MaxDraftBytes = 32_768` check bounds JSON parsing, validation CPU work, and repair-history growth *after* `IChatClient.GetResponseAsync` materializes the response string. Transport-level allocation bounds are governed separately by HTTP response buffers and model `MaxOutputTokens`.
- **Strict JSON Deserialization:**
  1. Scan UTF-8 bytes with `Utf8JsonReader` (`AllowTrailingCommas = false`, `CommentHandling = JsonCommentHandling.Disallow`), maintaining a stack of `HashSet<string>(StringComparer.OrdinalIgnoreCase)` per JSON object so duplicate keys (e.g., `{"status":"answered","status":"could_not_answer"}`) are rejected deterministically regardless of serializer behavior.
  2. Deserialize using `StrictJsonOptions` (`PropertyNameCaseInsensitive = true`, `UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow`, `AllowTrailingCommas = false`, `ReadCommentHandling = JsonCommentHandling.Disallow`), paired with `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]` on all draft record types.
- **Checkable Truncation Disclosure:** When `ledger.HasPaginationTruncation == true`, `draft.Limitations` must be non-empty **and** contain at least one explicit truncation disclosure marker (`"paginat"`, `"truncat"`, `"hasmore"`, `"more results"`, `"additional titles"`, `"partial"`, `"page limit"`, `"incomplete"`). Unrelated limitation notes (such as `"Sales figures exclude tax."`) fail validation.
- **Structural Provenance vs. Semantic Entailment:** Validation verifies IDs, `(bookId, editionId)` pairs, returned fields, and `(documentId, chunkId)` links against `EvidenceLedger`, plus scoped empty-search authorization—not free-text factual entailment of `Answer` or `Claim`.

### Implementation Sketch (`AgentOutputValidator.cs`)

```csharp
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PublishingTracker.Agent.Contracts;
using PublishingTracker.Agent.Orchestration;

namespace PublishingTracker.Agent.Validation;

public sealed record AgentValidationResult(
    bool IsValid,
    AgentModelDraft? Draft,
    string? Error)
{
    public static AgentValidationResult Success(AgentModelDraft draft) => new(true, draft, null);
    public static AgentValidationResult Fail(string error) => new(false, null, error);
}

public sealed class AgentOutputValidator
{
    public const int MaxDraftBytes = 32_768;

    private static readonly string[] TruncationDisclosureMarkers =
    [
        "paginat", "truncat", "hasmore", "more results",
        "additional titles", "partial", "page limit", "incomplete"
    ];

    private static readonly JsonSerializerOptions StrictJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow
    };

    public AgentValidationResult Validate(string? rawJson, EvidenceLedger ledger)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return AgentValidationResult.Fail("Output is empty; expected JSON matching AgentModelDraft.");

        var utf8Bytes = Encoding.UTF8.GetBytes(rawJson);
        if (utf8Bytes.Length > MaxDraftBytes)
            return AgentValidationResult.Fail($"Output exceeds maximum draft size of {MaxDraftBytes} bytes.");

        if (!TryValidateStrictJsonNoDuplicateKeys(utf8Bytes, out var duplicateOrSyntaxError))
            return AgentValidationResult.Fail(duplicateOrSyntaxError);

        AgentModelDraft? draft;
        try
        {
            draft = JsonSerializer.Deserialize<AgentModelDraft>(utf8Bytes, StrictJsonOptions);
        }
        catch (JsonException)
        {
            return AgentValidationResult.Fail("Output is not valid strict JSON matching AgentModelDraft (check required types and remove unknown properties).");
        }

        if (draft is null)
            return AgentValidationResult.Fail("Output deserialized to null.");

        if (draft.Status is not ("answered" or "could_not_answer"))
            return AgentValidationResult.Fail("Status must be 'answered' or 'could_not_answer'.");

        if (string.IsNullOrWhiteSpace(draft.Answer))
            return AgentValidationResult.Fail("Answer must be a non-empty string.");

        if (draft.Evidence is null || draft.Evidence.CatalogueSources is null || draft.Evidence.DocumentSources is null)
            return AgentValidationResult.Fail("Evidence object and source collections must not be null.");

        foreach (var cat in draft.Evidence.CatalogueSources)
        {
            if (cat is null || cat.BookId <= 0)
                return AgentValidationResult.Fail("Catalogue citation must provide a positive integer bookId.");

            if (cat.EditionId is <= 0)
                return AgentValidationResult.Fail($"Invalid EditionId={cat.EditionId}; must be positive when supplied.");

            if (string.IsNullOrWhiteSpace(cat.Claim))
                return AgentValidationResult.Fail($"Catalogue citation for BookId={cat.BookId} is missing a claim.");

            if (cat.Fields is null || cat.Fields.Count == 0 || cat.Fields.Any(string.IsNullOrWhiteSpace))
                return AgentValidationResult.Fail($"Catalogue citation for BookId={cat.BookId} must list at least one non-empty field.");

            if (!ledger.TryValidateCatalogueCitation(cat.BookId, cat.EditionId, cat.Fields, out var ledgerError))
                return AgentValidationResult.Fail(ledgerError);
        }

        foreach (var doc in draft.Evidence.DocumentSources)
        {
            if (doc is null || string.IsNullOrWhiteSpace(doc.DocumentId) ||
                string.IsNullOrWhiteSpace(doc.ChunkId) || string.IsNullOrWhiteSpace(doc.Claim))
            {
                return AgentValidationResult.Fail("Document citations must provide non-empty documentId, chunkId, and claim.");
            }

            if (!ledger.HasDocumentEvidence(doc.DocumentId, doc.ChunkId))
                return AgentValidationResult.Fail($"Uncorroborated document link: DocumentId='{doc.DocumentId}', ChunkId='{doc.ChunkId}'.");
        }

        var totalCitations = draft.Evidence.CatalogueSources.Count + draft.Evidence.DocumentSources.Count;
        if (draft.Status == "answered" && totalCitations == 0)
        {
            if (!ledger.CanAuthorizeZeroCitationAnswer(draft.Answer, out var emptySearchError))
                return AgentValidationResult.Fail(emptySearchError);
        }

        if (ledger.HasPaginationTruncation)
        {
            if (string.IsNullOrWhiteSpace(draft.Limitations) ||
                !TruncationDisclosureMarkers.Any(marker => draft.Limitations.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                return AgentValidationResult.Fail(
                    "Pagination truncation occurred (hasMore=true); 'limitations' must explicitly disclose that catalogue results were truncated or paginated.");
            }
        }

        return AgentValidationResult.Success(draft);
    }

    private static bool TryValidateStrictJsonNoDuplicateKeys(byte[] utf8Bytes, out string error)
    {
        try
        {
            var reader = new Utf8JsonReader(utf8Bytes, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });

            var keyScopes = new Stack<HashSet<string>>();
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        keyScopes.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                        break;
                    case JsonTokenType.PropertyName:
                        var name = reader.GetString() ?? string.Empty;
                        if (keyScopes.Count > 0 && !keyScopes.Peek().Add(name))
                        {
                            error = $"JSON draft contains duplicate property '{name}'.";
                            return false;
                        }
                        break;
                    case JsonTokenType.EndObject:
                        if (keyScopes.Count > 0) keyScopes.Pop();
                        break;
                }
            }

            error = string.Empty;
            return true;
        }
        catch (JsonException)
        {
            error = "Output is not valid strict JSON (comments and trailing commas are disallowed).";
            return false;
        }
    }
}
```

---

## 5. Workstream 3 — Two-Stage Execution, Timeouts, & Repair Ordering (`AgentLoopRunner.cs`)

### 5.1 Stage 1 & Stage 2 Shared Budgets
- **Stage 1 Budget Reserve:** Because Stage 2 requires at least one turn to emit `AgentModelDraft`, Stage 1 is capped at `MaxModelTurns - 1` (`5` turns). If Stage 1 emits function calls on turn `MaxModelTurns - 1` (`turn == 5`), it executes those tools and proceeds cleanly; if Stage 1 still emits function calls at the start of turn `MaxModelTurns` (`turn == 6`), it halts immediately with `AgentErrorCode.TURN_LIMIT_EXCEEDED`.
- **Fail-Closed Evidence Indexing:** Immediately after tool payload serialization & size checks pass (`<= MaxToolPayloadBytes`), call `ledger.TryRecordToolOutput(...)`. If parsing or pagination-sequence math fails, log internally and fail the run immediately with `errorCode` (`EVIDENCE_INDEXING_ERROR` or `TOOL_ERROR`).

### 5.2 Stage 2 Per-Call Timeouts & Repair Conversation Ordering
Every Stage 2 model call enforces `_options.PerCallTimeout` (`20s`) linked to `runToken` (`60s` overall deadline), and maintains strict `User -> Assistant -> User` message alternation across repair turns:

```csharp
// Conversation ordering: Stage 1 ended with an Assistant text message already in `messages`.
// Append the Stage 2 User formatting instruction once before attempt = 0:
messages.Add(new ChatMessage(ChatRole.User, AgentPrompts.Stage2FormattingPrompt));

var schemaOptions = new ChatOptions
{
    Tools = null, // Never mix tools and strict ResponseFormat in the same turn (ADR-007)
    ResponseFormat = ChatResponseFormat.ForJsonSchema<AgentModelDraft>()
};

for (var attempt = 0; attempt <= _options.MaxValidationRetries; attempt++)
{
    if (modelTurns >= _options.MaxModelTurns)
    {
        return FinishFailure(
            AgentErrorCode.TURN_LIMIT_EXCEEDED,
            ledger.ToolsUsed, modelTurns, totalToolCalls, ledger.HasPaginationTruncation, runActivity, stopwatch);
    }

    modelTurns++;
    var stage2Watch = Stopwatch.StartNew();
    using var turnActivity = AgentTelemetry.Source.StartActivity("agent.model_turn");
    turnActivity?.SetTag("agent.turn_number", modelTurns);
    turnActivity?.SetTag("agent.stage", "structured_output");
    turnActivity?.SetTag("agent.validation_attempt", attempt);

    using var stage2CallCts = CancellationTokenSource.CreateLinkedTokenSource(runToken);
    stage2CallCts.CancelAfter(_options.PerCallTimeout);

    ChatResponse stage2Response;
    try
    {
        stage2Response = await _chatClient.GetResponseAsync(messages, schemaOptions, stage2CallCts.Token);
        turnActivity?.SetTag("agent.duration_ms", stage2Watch.ElapsedMilliseconds);
    }
    catch (OperationCanceledException)
    {
        var code = cancellationToken.IsCancellationRequested ? AgentErrorCode.CANCELLED : AgentErrorCode.TIMEOUT;
        turnActivity?.SetTag("agent.error_code", code.ToString());
        return FinishFailure(
            code, ledger.ToolsUsed, modelTurns, totalToolCalls, ledger.HasPaginationTruncation, runActivity, stopwatch);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Stage 2 structured model call failed on turn {Turn}, attempt {Attempt}.", modelTurns, attempt);
        turnActivity?.SetTag("agent.error_code", nameof(AgentErrorCode.MODEL_ERROR));
        return FinishFailure(
            AgentErrorCode.MODEL_ERROR, ledger.ToolsUsed, modelTurns, totalToolCalls, ledger.HasPaginationTruncation, runActivity, stopwatch);
    }

    var validation = _validator.Validate(stage2Response.Text, ledger);
    turnActivity?.SetTag("agent.validation_passed", validation.IsValid);

    if (validation.IsValid && validation.Draft is not null)
    {
        runActivity?.SetTag("agent.validation_retries_used", attempt);
        runActivity?.SetTag("agent.status", "completed");
        runActivity?.SetTag("agent.total_tool_calls", totalToolCalls);
        runActivity?.SetTag("agent.duration_ms", stopwatch.ElapsedMilliseconds);

        return AgentQueryResponse.Answered(
            validation.Draft,
            ledger.HasPaginationTruncation,
            ledger.ToolsUsed,
            modelTurns,
            totalToolCalls);
    }

    if (attempt == _options.MaxValidationRetries)
    {
        _logger.LogWarning("Stage 2 validation exhausted retries: {ValidationError}", validation.Error);
        return FinishFailure(
            AgentErrorCode.VALIDATION_RETRIES_EXHAUSTED,
            ledger.ToolsUsed, modelTurns, totalToolCalls, ledger.HasPaginationTruncation, runActivity, stopwatch);
    }

    // Strict User -> Assistant -> User ordering before the next repair turn:
    messages.Add(new ChatMessage(ChatRole.Assistant, stage2Response.Text ?? string.Empty));
    messages.Add(new ChatMessage(ChatRole.User,
        $"Validation failed: {validation.Error}. Return valid JSON matching the schema citing only linked evidence observed in the tool results above."));
}
```

### 5.3 Privacy-Safe Stage 2 Telemetry Tags
- `agent.model_turn`: `agent.stage` (`"tool_selection"` / `"structured_output"`), `agent.validation_attempt` (`0..2`), `agent.validation_passed` (`true` / `false`), `agent.duration_ms`, `agent.error_code` (on error).
- `agent.run`: `agent.validation_retries_used`, `agent.status`, `agent.total_tool_calls`, `agent.duration_ms`, `agent.error_code`.
- `EnableSensitiveData = false` remains enforced: zero draft JSON text, claims, answers, or validation diagnostics appear on OpenTelemetry spans.

---

## 6. Workstream 4 — Deterministic Phase 3 Test Suite (`Phase3EvidenceAndValidationTests.cs`)

Add `tests/PublishingTracker.Agent.Tests/Phase3EvidenceAndValidationTests.cs` exercising all **22 deterministic scenarios** via `ScriptedChatClient` with zero external API calls:

1. **`Stage2_ValidAnsweredResponse_WithLinkedBookAndEditionCitations_SucceedsAndInjectsHostToolsUsed`**
   - Stage 1 calls `find_titles_missing_metadata` + `get_title_details`; Stage 2 returns valid `AgentModelDraft` citing `(BookId = 42, EditionId = 105, Fields = ["Narrator", "Duration"])`.
   - Assert `SchemaVersion == "1.0"`, `Status == "answered"`, and `ToolsUsed` equals host `ToolExecutionRecord` timings.
2. **`Stage2_ValidCouldNotAnswerResponse_WithZeroCitations_Succeeds`**
   - Stage 2 returns `Status = "could_not_answer"`, empty citations, and clear explanation.
3. **`Stage2_AnsweredWithZeroCitations_AllowedWhenSingleScopedCatalogueSearchReturnedZeroItems_AndAnswerMentionsFilter`**
   - Stage 1 calls `find_titles_missing_metadata(format: "Audiobook")` returning `totalCount: 0, items: []`; Stage 2 emits `Status = "answered"`, zero citations, and mentions `"Audiobook"` in `Answer`.
   - Assert validation passes.
4. **`Stage2_AnsweredWithZeroCitations_RejectedWhenEmptyFilteredSearchFollowedByNonEmptySearch`**
   - Stage 1 calls `find_titles_missing_metadata(format: "Audiobook")` (`totalCount: 0`) AND `find_titles_missing_metadata(format: "Ebook")` (returns Book 12); Stage 2 emits `Status = "answered"` with zero citations across all retries.
   - Assert empty search on `Audiobook` cannot justify an uncited answer and run fails with `VALIDATION_RETRIES_EXHAUSTED`.
5. **`Stage2_AnsweredWithZeroCitations_RejectedWhenAnswerOmitsFilterOfEmptySearch`**
   - Stage 1 executes `find_titles_missing_metadata(format: "Audiobook")` returning `totalCount: 0`; Stage 2 emits `"Your entire catalogue is complete."` omitting `"Audiobook"`.
   - Assert validator rejects the unqualified uncited claim until repaired.
6. **`Validator_MismatchedBookAndEditionPair_RejectedAndRepairedOnRetry`**
   - Stage 1 indexes Book 1 (Edition 10) and Book 2 (Edition 20). Stage 2 attempt 0 cites `(BookId = 1, EditionId = 20)`; attempt 1 repairs to `(BookId = 2, EditionId = 20)`.
   - Assert attempt 0 fails provenance check and attempt 1 succeeds.
7. **`Validator_UnreturnedFieldCitation_RejectedAndRepairedOnRetry`**
   - Stage 1 calls only `find_titles_missing_metadata` (never calling `get_title_details`, so `BasePrice` was not returned); Stage 2 attempt 0 cites `["BasePrice"]`; attempt 1 repairs to `["Narrator"]`.
8. **`Validator_BookLevelAndEditionLevelFieldsCombined_AcceptedForLinkedEdition`**
   - Stage 1 returns Book 42 + Edition 105 via `get_title_details`; Stage 2 cites `(BookId = 42, EditionId = 105, Fields = ["Title", "Narrator"])`.
9. **`Validator_UncorroboratedDocumentCitation_Rejected`**
   - Stage 2 cites `(DocumentId = "doc-guide", ChunkId = "doc-guide#c99")` which is absent from `EvidenceLedger`.
10. **`Validator_MalformedJsonOnAttempt0_RepairedOnAttempt1_Succeeds_AndPreservesAlternatingMessageOrder`**
    - Stage 2 attempt 0 returns invalid JSON; attempt 1 returns valid draft.
    - Inspect `ScriptedChatClient.Invocations` and assert exact trailing message sequence on attempt 1: `[Stage 1 Assistant] -> [Stage2FormattingPrompt (User)] -> [Invalid Draft (Assistant)] -> [Repair Prompt (User)]`.
11. **`Validator_DuplicateJsonPropertyNames_AndUnmappedProperties_Rejected`**
    - Test raw JSON containing duplicate keys (`{"status":"answered","status":"could_not_answer",...}`) and test raw JSON containing an unmapped property (`"toolsUsed": ["fake"]`).
    - Assert both fail strict validation deterministically.
12. **`Validator_DraftByteSizeBoundary_32768BytesAccepted_32769BytesRejected`**
    - Assert exact UTF-8 byte boundary: a valid draft JSON totaling exactly `32,768` bytes passes `MaxDraftBytes`, while `32,769` bytes (including multi-byte UTF-8 characters) fails size guard.
13. **`Validator_NullCollectionsAndMalformedCitationFields_Rejected`**
    - Exercise `Evidence = null`, `CatalogueSources = null`, `BookId = 0`, `EditionId = -1`, blank `Fields`, empty `Claim`, and non-contract `Status = "partial"`.
14. **`Validator_UnrelatedLimitationsWhenPaginationTruncated_Rejected_UntilTruncationDisclosed`**
    - Stage 1 receives `hasMore: true`. Stage 2 attempt 0 emits unrelated `Limitations = "Sales figures exclude tax."`; attempt 1 discloses pagination truncation.
    - Assert attempt 0 fails with truncation disclosure error, attempt 1 passes, and host `AgentQueryResponse.Limitations` includes canonical disclosure.
15. **`Pagination_OutOfOrderPage2First_OrMidQueryPageSizeChange_HaltsWithPaginationSequenceError`**
    - Case A: Model requests `find_titles_missing_metadata(page: 2)` first $\to$ halts before tool execution with `PAGINATION_SEQUENCE_ERROR`.
    - Case B: Model fetches `page: 1, pageSize: 10` (`hasMore: true`), then requests `page: 2, pageSize: 20` $\to$ halts with `PAGINATION_SEQUENCE_ERROR`.
16. **`Pagination_InconsistentReturnedPageOrTotalCount_HaltsWithEvidenceIndexingError`**
    - Tool returns `page: 2` when `page: 1` was requested, OR returns `totalCount: 50, hasMore: false` with only 2 items on page 1.
    - Assert runner halts immediately with `EVIDENCE_INDEXING_ERROR`.
17. **`EvidenceLedger_UnparseableOrMalformedToolPayload_HaltsWithEvidenceIndexingError`**
    - Stub `find_titles_missing_metadata` returns `{ "unexpected": true }` (missing `items`, `hasMore`, etc.).
    - Assert `TryRecordToolOutput` fails closed with `EVIDENCE_INDEXING_ERROR` instead of silently continuing.
18. **`EvidenceLedger_RealMcpCallToolResultJsonElement_UnwrapsStructuredContentCorrectly`**
    - Stub tool returns a `JsonElement` matching real `ModelContextProtocol.Protocol.CallToolResult` (`structuredContent` + `content` + `isError: false`).
    - Assert `EvidenceLedger` unwraps, indexes fields cleanly, and enables Stage 2 citation verification.
19. **`Stage2_PerCallTimeoutOnStructuredTurn_HaltsWithTimeoutErrorCode`**
    - Delay `ScriptedChatClient` beyond `PerCallTimeout` on Stage 2 attempt 0 while `OverallTimeout` remains larger.
    - Assert run halts immediately with `TIMEOUT` public error code.
20. **`Stage2_ValidationRetriesExhausted_FailsWithApprovedErrorCodeAndPreservesHostToolsUsed`**
    - Stage 2 returns invalid JSON on attempts 0, 1, and 2.
    - Assert 3 Stage 2 turns executed, run fails with `VALIDATION_RETRIES_EXHAUSTED`, and `ToolsUsed` preserves Stage 1 executions.
21. **`Stage2_RepairRetryHitsMaxModelTurnsCeiling_HaltsWithTurnLimitExceeded`**
    - Stage 1 consumes 5 turns; Stage 2 attempt 0 consumes turn 6 and fails validation.
    - Assert repair attempt 1 does **not** invoke `_chatClient` and run returns `TURN_LIMIT_EXCEEDED`.
22. **`FailureEnvelope_ConstrainedToApprovedErrorCodes_NeverLeaksInternalDiagnosticStrings`**
    - Force tool/indexing exceptions containing sensitive internal markers (`"Server=internal-pg-01;SecretPassword123"`).
    - Assert `AgentQueryResponse.Failure(...)` output contains only the approved `AgentErrorCode` prefix/summary and zero sensitive diagnostic text.

---

## 7. Execution Sequence & Acceptance Checklist

### Step-by-Step Implementation Sequence
1. **Step 3.1 — Contracts, Error Taxonomy, & Upgrade Alignment:**
   - Add `AgentContracts.cs` (`AgentErrorCode`, `AgentErrorCatalog`, `[JsonUnmappedMemberHandling(Disallow)]` drafts, `ToolExecutionRecord`, versioned `AgentQueryResponse`).
   - Update `AgentRunResult` and `Program.cs` (`POST /api/agent/query`) to emit `AgentQueryResponse` `"1.0"`.
2. **Step 3.2 — Fail-Closed `EvidenceLedger`:**
   - Implement `EvidenceLedger.cs` with real `CallToolResult` unwrapping, pre/post pagination sequence checks, scoped empty-search tracking, and linked `(bookId, editionId?) -> fields` / `(documentId, chunkId)` indexing.
3. **Step 3.3 — Strict `AgentOutputValidator`:**
   - Implement `AgentOutputValidator.cs` with post-materialization `32,768`-byte bound, `Utf8JsonReader` duplicate-key check, `StrictJsonOptions`, provenance verification, and checkable truncation disclosure.
4. **Step 3.4 — Two-Stage `AgentLoopRunner` & Stage 2 Telemetry:**
   - Update `AgentLoopRunner.cs` to reserve turn budget for Stage 2, apply `PerCallTimeout` on Stage 2 turns, maintain `User -> Assistant -> User` repair ordering, fail closed on `TryRecordToolOutput` errors, and emit Stage 2 telemetry tags.
5. **Step 3.5 — Deterministic Test Suite:**
   - Implement all 22 scenarios in `tests/PublishingTracker.Agent.Tests/Phase3EvidenceAndValidationTests.cs`.
6. **Step 3.6 — Build & Verification:**
   - Run `dotnet build` and `dotnet test` across `PublishingTracker.Api.sln` confirming Phases 0–3 pass with zero external calls.

### Phase 3 Acceptance Checklist
- [x] `AgentQueryResponse.Failure` accepts only `AgentErrorCode`; internal exception details are never emitted to API callers
- [x] Draft types (`AgentModelDraft`, `AgentEvidenceDraft`, citations) enforce `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]`, and `AgentOutputValidator` rejects duplicate JSON keys and >32,768-byte post-materialization drafts
- [x] `EvidenceLedger` unwraps real MCP `CallToolResult` envelopes cleanly and halts fail-closed with `EVIDENCE_INDEXING_ERROR` on unparseable or malformed tool outputs
- [x] Sequential pagination rules reject out-of-order first pages (`PAGINATION_SEQUENCE_ERROR`), mid-query page-size changes, calls after `hasMore=false`, and inconsistent `totalCount`/`page` responses
- [x] Zero-citation `"answered"` responses are authorized only for a single scoped empty search with zero indexed books/chunks and filter echoed in `Answer`
- [x] Pagination truncation (`hasMore=true`) rejects unrelated `Limitations` text until explicit truncation disclosure is present
- [x] Stage 2 applies `PerCallTimeout`, separates tools from `ResponseFormat`, reserves Stage 2 budget within `MaxModelTurns`, and preserves `User -> Assistant -> User` repair ordering
- [x] Provenance validation explicitly documents its boundary (source/field linkage only, not semantic entailment)
- [x] All 22 deterministic Phase 3 test scenarios pass in CI without external spend (`42/42` Agent tests, `96/96` solution tests passing)

### Implementation Notes & Verified Adjustments
1. **Verified `ChatResponseFormat.ForJsonSchema<AgentModelDraft>(JsonSerializerOptions.Web)`:** Confirmed against `Microsoft.Extensions.AI.Abstractions` `10.10.0` so Stage 2 requests camelCase schemas matching `StrictJsonOptions` (`PropertyNameCaseInsensitive = true`, `UnmappedMemberHandling.Disallow`, `AllowTrailingCommas = false`, `ReadCommentHandling = JsonCommentHandling.Disallow`).
2. **Async Loop State Disposal (`LoopRunState : IAsyncDisposable`):** Updated `AgentLoopRunner` so `IMcpToolSession`, `RunCts`, and `RunActivity` dispose asynchronously in `finally` blocks prior to returning results, ensuring OpenTelemetry `agent.run` activities stop before task completion.
3. **Phase 2 Backwards Compatibility:** Preserved `AgentLoopRunner.RunAsync` for the 20 existing Phase 2 unit tests while switching `POST /api/agent/query` in `PublishingTracker.Agent/Program.cs` to `RunVerifiedAsync`, backed by `EvidenceLedger` and `AgentOutputValidator`.
