using System.Globalization;
using System.Text.Json;
using PublishingTracker.Agent.Contracts;
using PublishingTracker.Application.Books.Queries;
using PublishingTracker.Rag.Contracts;
using PublishingTracker.Rag.Services;

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
        "Dimensions", "FileSize", "Narrator", "Duration", "MissingFields", "PlatformSummaries"
    ];

    private static readonly string[] TitleDetailsPlatformSummaryFields =
    [
        "PlatformId", "PlatformName", "IsPublished", "PublishedDate", "PlatformProductId"
    ];

    private readonly Dictionary<CatalogueEvidenceKey, HashSet<string>> _catalogue = new();
    private readonly Dictionary<DocumentEvidenceKey, string> _documents = new();
    private readonly Dictionary<string, PaginationQueryState> _paginationByFilter = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CatalogueSearchRecord> _searchesByFilter = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ToolExecutionRecord> _toolsUsed = new();

    private int _totalCataloguePagesFetched;
    private int _ragSearchesExecuted;

    public IReadOnlyList<ToolExecutionRecord> ToolsUsed => _toolsUsed;
    public bool HasPaginationTruncation => _paginationByFilter.Values.Any(state => state.HasMore);

    public void RecordToolExecution(string toolName, int turn, long durationMs, bool succeeded) =>
        _toolsUsed.Add(new ToolExecutionRecord(toolName, turn, durationMs, succeeded));

    public bool TryValidateToolRequest(
        string toolName,
        IDictionary<string, object?>? arguments,
        int maxPaginationPages,
        out AgentErrorCode errorCode,
        out string diagnosticError)
    {
        if (string.Equals(toolName, "search_publishing_guidelines", StringComparison.Ordinal))
            return TryValidateRagSearchRequest(arguments, out errorCode, out diagnosticError);

        return TryValidatePaginationRequest(toolName, arguments, maxPaginationPages, out errorCode, out diagnosticError);
    }

    private static bool TryValidateRagSearchRequest(
        IDictionary<string, object?>? arguments,
        out AgentErrorCode errorCode,
        out string diagnosticError)
    {
        if (arguments is null || !arguments.TryGetValue("query", out var rawQuery) ||
            !TryExtractStringArgument(rawQuery, out var query) ||
            string.IsNullOrWhiteSpace(query) || query.Trim().Length > RagRetrievalService.MaxQueryLength)
        {
            errorCode = AgentErrorCode.INVALID_TOOL_ARGUMENTS;
            diagnosticError = $"Tool argument 'query' must be a non-empty string up to {RagRetrievalService.MaxQueryLength} characters.";
            return false;
        }

        if (!TryExtractIntArgument(arguments, "topK", defaultValue: 5, out var topK) ||
            topK < 1 || topK > RagRetrievalService.MaxTopK)
        {
            errorCode = AgentErrorCode.INVALID_TOOL_ARGUMENTS;
            diagnosticError = $"Tool argument 'topK' must be an integer between 1 and {RagRetrievalService.MaxTopK}.";
            return false;
        }

        errorCode = default;
        diagnosticError = string.Empty;
        return true;
    }

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

        if (!TryExtractIntArgument(arguments, "page", defaultValue: 1, out var page))
        {
            errorCode = AgentErrorCode.INVALID_TOOL_ARGUMENTS;
            diagnosticError = "Tool argument 'page' must be a valid integer.";
            return false;
        }

        if (!TryExtractIntArgument(arguments, "pageSize", defaultValue: DefaultPageSize, out var pageSize) ||
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
                return false;

            return TryIndexMissingMetadataPage(arguments, pageDto, out errorCode, out diagnosticError);
        }

        if (string.Equals(toolName, "get_title_details", StringComparison.Ordinal))
        {
            if (!TryUnwrapTitleDetails(rawResult, serializedJson, out var detailsDto, out errorCode, out diagnosticError) || detailsDto is null)
                return false;

            return TryIndexTitleDetails(arguments, detailsDto, out errorCode, out diagnosticError);
        }

        if (string.Equals(toolName, "search_publishing_guidelines", StringComparison.Ordinal))
        {
            if (!TryUnwrapRagSearchResult(rawResult, serializedJson, out var ragDto, out errorCode, out diagnosticError) || ragDto is null)
                return false;

            return TryIndexRagSearchResult(arguments, ragDto, out errorCode, out diagnosticError);
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

        if (_catalogue.Count > 0 || _documents.Count > 0 || _ragSearchesExecuted > 0)
        {
            error = "Status 'answered' requires citations whenever catalogue titles or guideline searches occurred in the run.";
            return false;
        }

        var onlySearch = _searchesByFilter.Values.Single();
        if (onlySearch.TotalCount != 0 || onlySearch.ItemsReturned != 0)
        {
            error = "Status 'answered' requires at least one corroborated evidence entry.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(onlySearch.Format) &&
            !AnswerMentionsFilter(answerText, onlySearch.Format))
        {
            error = $"Uncited empty-search answer must reference the queried format filter '{onlySearch.Format}'.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(onlySearch.MissingField) &&
            !AnswerMentionsFilter(answerText, onlySearch.MissingField))
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
        TryExtractIntArgument(arguments, "page", defaultValue: 1, out var requestedPage);
        TryExtractIntArgument(arguments, "pageSize", defaultValue: DefaultPageSize, out var requestedPageSize);

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
        IDictionary<string, object?>? arguments,
        TitleDetailsDto details,
        out AgentErrorCode errorCode,
        out string diagnosticError)
    {
        if (arguments is not null && arguments.TryGetValue("bookId", out var rawBookId) && rawBookId is not null)
        {
            if (!TryExtractIntArgument(arguments, "bookId", details.BookId, out var requestedBookId) ||
                requestedBookId != details.BookId)
            {
                errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
                diagnosticError = $"Returned TitleDetailsDto BookId={details.BookId} does not match requested bookId argument.";
                return false;
            }
        }

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

            foreach (var summary in edition.PlatformSummaries)
            {
                if (summary is null || summary.PlatformId <= 0 || string.IsNullOrWhiteSpace(summary.PlatformName))
                {
                    errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
                    diagnosticError = $"TitleDetailsDto for BookId={details.BookId}, EditionId={edition.EditionId} contains malformed platform summary.";
                    return false;
                }
            }

            AddCatalogueFields(details.BookId, edition.EditionId, TitleDetailsEditionFields);
            AddCatalogueFields(details.BookId, edition.EditionId, edition.MissingFields);
            if (edition.PlatformSummaries.Count > 0)
            {
                AddCatalogueFields(details.BookId, edition.EditionId, TitleDetailsPlatformSummaryFields);
            }
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

        return TryDeserializePayload(payloadElement, "MissingMetadataPageDto", out dto, out errorCode, out diagnosticError);
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

        return TryDeserializePayload(payloadElement, "TitleDetailsDto", out dto, out errorCode, out diagnosticError);
    }

    private static bool TryDeserializePayload<T>(
        JsonElement payloadElement,
        string typeName,
        out T? dto,
        out AgentErrorCode errorCode,
        out string diagnosticError)
    {
        try
        {
            dto = payloadElement.Deserialize<T>(JsonSerializerOptions.Web);
            if (dto is null)
            {
                errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
                diagnosticError = $"Deserialized {typeName} was null.";
                return false;
            }

            errorCode = default;
            diagnosticError = string.Empty;
            return true;
        }
        catch (JsonException ex)
        {
            dto = default;
            errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
            diagnosticError = $"Failed to deserialize {typeName}: {ex.Message}";
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
                contentProp.GetArrayLength() > 0 &&
                contentProp[0].ValueKind == JsonValueKind.Object)
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
            if (!allowedFields.Contains(field.Trim()))
            {
                var scope = editionId is int eid ? $"BookId={bookId}, EditionId={eid}" : $"BookId={bookId}";
                error = $"Unreturned field '{field}' cited for {scope}.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool AnswerMentionsFilter(string answerText, string filterValue)
    {
        if (answerText.Contains(filterValue, StringComparison.OrdinalIgnoreCase))
            return true;

        if (filterValue.Equals("ISBN_Or_PlatformProductId", StringComparison.OrdinalIgnoreCase))
        {
            return answerText.Contains("ISBN", StringComparison.OrdinalIgnoreCase) ||
                   answerText.Contains("platform product", StringComparison.OrdinalIgnoreCase);
        }

        var spaced = HumanizeIdentifier(filterValue);
        if (answerText.Contains(spaced, StringComparison.OrdinalIgnoreCase))
            return true;

        return spaced.EndsWith(" Url", StringComparison.OrdinalIgnoreCase) &&
               answerText.Contains(spaced[..^4], StringComparison.OrdinalIgnoreCase);
    }

    private static string HumanizeIdentifier(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '_')
            {
                builder.Append(' ');
                continue;
            }

            if (i > 0 && char.IsUpper(ch) && char.IsLower(value[i - 1]))
                builder.Append(' ');

            builder.Append(ch);
        }

        return builder.ToString();
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

    private static bool TryExtractIntArgument(
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

        if (raw is long l && l is >= int.MinValue and <= int.MaxValue)
        {
            value = (int)l;
            return true;
        }

        if (raw is JsonElement elem)
            return elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out value);

        return raw is string text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryExtractStringArgument(object? value, out string result)
    {
        if (value is string s)
        {
            result = s;
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.String } jsonString)
        {
            result = jsonString.GetString() ?? string.Empty;
            return true;
        }

        result = string.Empty;
        return false;
    }

    private static bool TryUnwrapRagSearchResult(
        object? rawResult,
        string serializedJson,
        out RagSearchResultDto? ragResult,
        out AgentErrorCode errorCode,
        out string diagnosticError)
    {
        if (rawResult is RagSearchResultDto typedDto)
        {
            ragResult = typedDto;
            errorCode = default;
            diagnosticError = string.Empty;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(serializedJson))
        {
            try
            {
                ragResult = JsonSerializer.Deserialize<RagSearchResultDto>(serializedJson, JsonSerializerOptions.Web);
                if (ragResult is not null)
                {
                    errorCode = default;
                    diagnosticError = string.Empty;
                    return true;
                }
            }
            catch (JsonException ex)
            {
                errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
                diagnosticError = $"Failed to deserialize search_publishing_guidelines payload: {ex.Message}";
                ragResult = null;
                return false;
            }
        }

        errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
        diagnosticError = "search_publishing_guidelines returned an unrecognized or empty payload.";
        ragResult = null;
        return false;
    }

    private bool TryIndexRagSearchResult(
        IDictionary<string, object?>? arguments,
        RagSearchResultDto ragDto,
        out AgentErrorCode errorCode,
        out string diagnosticError)
    {
        if (string.IsNullOrWhiteSpace(ragDto.Query) || ragDto.Hits is null)
        {
            errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
            diagnosticError = "search_publishing_guidelines result missing required Query or Hits collection.";
            return false;
        }

        TryExtractIntArgument(arguments, "topK", defaultValue: 5, out var requestedTopK);
        if (ragDto.TopK != requestedTopK || ragDto.Hits.Count > ragDto.TopK)
        {
            errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
            diagnosticError = $"search_publishing_guidelines returned topK {ragDto.TopK} or count {ragDto.Hits.Count} exceeding requested {requestedTopK}.";
            return false;
        }

        var seenChunkKeys = new HashSet<string>(StringComparer.Ordinal);
        var validated = new List<(string DocumentId, string ChunkId, string Excerpt)>(ragDto.Hits.Count);
        foreach (var hit in ragDto.Hits)
        {
            if (string.IsNullOrWhiteSpace(hit.DocumentId) ||
                string.IsNullOrWhiteSpace(hit.ChunkId) ||
                string.IsNullOrWhiteSpace(hit.Title) ||
                string.IsNullOrWhiteSpace(hit.Excerpt) ||
                !double.IsFinite(hit.CosineDistance) || hit.CosineDistance < 0)
            {
                errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
                diagnosticError = $"search_publishing_guidelines hit '{hit.ChunkId}' contains invalid or missing metadata.";
                return false;
            }

            var docId = hit.DocumentId.Trim();
            var chunkId = hit.ChunkId.Trim();
            if (!chunkId.StartsWith($"{docId}#c", StringComparison.Ordinal))
            {
                errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
                diagnosticError = $"ChunkId '{chunkId}' does not belong to DocumentId '{docId}'.";
                return false;
            }

            var chunkKey = $"{docId}::{chunkId}";
            if (!seenChunkKeys.Add(chunkKey))
            {
                errorCode = AgentErrorCode.EVIDENCE_INDEXING_ERROR;
                diagnosticError = $"Duplicate chunk '{chunkId}' found in search_publishing_guidelines hits.";
                return false;
            }

            validated.Add((docId, chunkId, hit.Excerpt));
        }

        // Second pass: only index once every hit has passed validation (no partial indexing).
        foreach (var (validDocId, validChunkId, excerpt) in validated)
            RecordDocumentHit(validDocId, validChunkId, excerpt);

        _ragSearchesExecuted++;
        errorCode = default;
        diagnosticError = string.Empty;
        return true;
    }
}
