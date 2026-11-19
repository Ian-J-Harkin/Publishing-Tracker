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
