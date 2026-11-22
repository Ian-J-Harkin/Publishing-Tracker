using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PublishingTracker.Agent.Configuration;
using PublishingTracker.Agent.Contracts;
using PublishingTracker.Agent.Mcp;
using PublishingTracker.Agent.Orchestration;
using PublishingTracker.Agent.Tests.Doubles;
using PublishingTracker.Agent.Validation;
using PublishingTracker.Application.Books.Queries;

namespace PublishingTracker.Agent.Tests;

public sealed class Phase3EvidenceAndValidationTests
{
    [Fact]
    public async Task Stage2_ValidAnsweredResponse_WithLinkedBookAndEditionCitations_SucceedsAndInjectsHostToolsUsed()
    {
        var provider = CreateCatalogueProvider(
            findHandler: (_, _, page, pageSize) => Task.FromResult<object?>(new MissingMetadataPageDto(
                page, pageSize, 1, false,
                [new MissingTitleItemDto(42, "Audiobook in Production", [], [new MissingEditionFieldsDto(105, "Audiobook", ["Narrator", "Duration"])])])),
            detailsHandler: _ => Task.FromResult<object?>(CreateTitleDetails(42, 105)));

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("format", "Audiobook")));
        chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", 42)));
        chat.EnqueueText("Audiobook 42 (edition 105) is missing Narrator and Duration.");
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "Audiobook 42 is missing Narrator and Duration on edition 105.",
            catalogue: [new CatalogueSourceCitation(42, 105, ["Narrator", "Duration"], "Edition 105 lacks Narrator and Duration.")]));

        var response = await Runner(chat, provider).RunVerifiedAsync("Check missing audiobook metadata.");

        Assert.Equal(AgentQueryResponse.CurrentSchemaVersion, response.SchemaVersion);
        Assert.Equal("answered", response.Status);
        Assert.Null(response.Error);
        Assert.Equal(4, response.Turns);
        Assert.Equal(2, response.TotalToolCalls);
        Assert.Equal(2, response.ToolsUsed.Count);
        Assert.Equal("find_titles_missing_metadata", response.ToolsUsed[0].ToolName);
        Assert.Equal(1, response.ToolsUsed[0].Turn);
        Assert.True(response.ToolsUsed[0].Succeeded);
        Assert.Equal("get_title_details", response.ToolsUsed[1].ToolName);
        Assert.Equal(2, response.ToolsUsed[1].Turn);
        Assert.True(response.ToolsUsed[1].Succeeded);
    }

    [Fact]
    public async Task Stage2_ValidCouldNotAnswerResponse_WithZeroCitations_Succeeds()
    {
        var chat = new ScriptedChatClient();
        chat.EnqueueText("I cannot determine historical pricing because no price-history tool exists.");
        chat.EnqueueText(SerializeDraft(
            status: "could_not_answer",
            answer: "Historical price data on a past date is not available in the read-only catalogue tools.",
            catalogue: []));

        var response = await Runner(chat, CreateCatalogueProvider()).RunVerifiedAsync("What was the price last year?");

        Assert.Equal(AgentQueryResponse.CurrentSchemaVersion, response.SchemaVersion);
        Assert.Equal("could_not_answer", response.Status);
        Assert.Null(response.Error);
        Assert.Empty(response.ToolsUsed);
        Assert.Empty(response.Evidence.CatalogueSources);
    }

    [Fact]
    public async Task Stage2_AnsweredWithZeroCitations_AllowedWhenSingleScopedCatalogueSearchReturnedZeroItems_AndAnswerMentionsFilter()
    {
        var provider = CreateCatalogueProvider(
            findHandler: (format, _, page, pageSize) => Task.FromResult<object?>(
                new MissingMetadataPageDto(page, pageSize, 0, false, [])));

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("format", "Audiobook")));
        chat.EnqueueText("No Audiobook titles are missing metadata.");
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "All Audiobook titles in your catalogue have complete metadata.",
            catalogue: []));

        var response = await Runner(chat, provider).RunVerifiedAsync("Are any audiobooks incomplete?");

        Assert.Equal("answered", response.Status);
        Assert.Null(response.Error);
        Assert.Contains("Audiobook", response.Answer);

        var seriesChat = new ScriptedChatClient();
        seriesChat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("missingField", "SeriesNumber")));
        seriesChat.EnqueueText("No titles are missing a series number.");
        seriesChat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "No titles are missing a series number.",
            catalogue: []));

        var seriesResponse = await Runner(seriesChat, provider).RunVerifiedAsync("Any missing series numbers?");

        Assert.Equal("answered", seriesResponse.Status);
        Assert.Null(seriesResponse.Error);
        Assert.Contains("series number", seriesResponse.Answer);
    }

    [Fact]
    public async Task Stage2_AnsweredWithZeroCitations_RejectedWhenEmptyFilteredSearchFollowedByNonEmptySearch()
    {
        var provider = CreateCatalogueProvider(
            findHandler: (format, _, page, pageSize) =>
            {
                if (string.Equals(format, "Audiobook", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult<object?>(new MissingMetadataPageDto(page, pageSize, 0, false, []));

                return Task.FromResult<object?>(new MissingMetadataPageDto(
                    page, pageSize, 1, false,
                    [new MissingTitleItemDto(12, "Incomplete Ebook", [], [new MissingEditionFieldsDto(212, "Ebook", ["FileSize"])])]));
            });

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("format", "Audiobook")));
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("format", "Ebook")));
        chat.EnqueueText("No incomplete Audiobook titles were found.");
        for (var i = 0; i <= 2; i++)
        {
            chat.EnqueueText(SerializeDraft(
                status: "answered",
                answer: "No Audiobook titles are missing metadata.",
                catalogue: []));
        }

        var response = await Runner(chat, provider).RunVerifiedAsync("Check Audiobook and Ebook gaps.");

        Assert.Equal("could_not_answer", response.Status);
        Assert.StartsWith("VALIDATION_RETRIES_EXHAUSTED:", response.Error);
        Assert.Equal(2, response.ToolsUsed.Count);
    }

    [Fact]
    public async Task Stage2_AnsweredWithZeroCitations_RejectedWhenAnswerOmitsFilterOfEmptySearch()
    {
        var provider = CreateCatalogueProvider(
            findHandler: (_, _, page, pageSize) => Task.FromResult<object?>(
                new MissingMetadataPageDto(page, pageSize, 0, false, [])));

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("format", "Audiobook")));
        chat.EnqueueText("Everything is complete.");
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "Your entire catalogue is complete.",
            catalogue: []));
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "No Audiobook titles are missing metadata.",
            catalogue: []));

        var response = await Runner(chat, provider).RunVerifiedAsync("Are any audiobooks incomplete?");

        Assert.Equal("answered", response.Status);
        Assert.Null(response.Error);
        Assert.Equal(4, response.Turns);
        Assert.Contains("Audiobook", response.Answer);
    }

    [Fact]
    public async Task Validator_MismatchedBookAndEditionPair_RejectedAndRepairedOnRetry()
    {
        var provider = CreateCatalogueProvider(
            findHandler: (_, _, page, pageSize) => Task.FromResult<object?>(new MissingMetadataPageDto(
                page, pageSize, 2, false,
                [
                    new MissingTitleItemDto(1, "First Book", [], [new MissingEditionFieldsDto(10, "Paperback", ["ISBN"])]),
                    new MissingTitleItemDto(2, "Second Book", [], [new MissingEditionFieldsDto(20, "Ebook", ["FileSize"])])
                ])));

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata"));
        chat.EnqueueText("Found gaps in books 1 and 2.");
        // Attempt 0 mixes Book 1 with Edition 20 (from Book 2)
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "Book 1 edition 20 is missing FileSize.",
            catalogue: [new CatalogueSourceCitation(1, 20, ["FileSize"], "Mismatched book/edition pair.")]));
        // Attempt 1 repairs to linked (2, 20)
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "Book 2 edition 20 is missing FileSize.",
            catalogue: [new CatalogueSourceCitation(2, 20, ["FileSize"], "Book 2 edition 20 lacks FileSize.")]));

        var response = await Runner(chat, provider).RunVerifiedAsync("Find missing fields.");

        Assert.Equal("answered", response.Status);
        Assert.Null(response.Error);
        Assert.Equal(4, response.Turns);
        Assert.Equal(2, Assert.Single(response.Evidence.CatalogueSources).BookId);
    }

    [Fact]
    public async Task Validator_UnreturnedFieldCitation_RejectedAndRepairedOnRetry()
    {
        var provider = CreateCatalogueProvider(
            findHandler: (_, _, page, pageSize) => Task.FromResult<object?>(new MissingMetadataPageDto(
                page, pageSize, 1, false,
                [new MissingTitleItemDto(42, "Audio Book", [], [new MissingEditionFieldsDto(105, "Audiobook", ["Narrator"])])])));

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("format", "Audiobook")));
        chat.EnqueueText("Book 42 lacks Narrator.");
        // find_titles_missing_metadata does not return BasePrice
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "Book 42 edition 105 has price info missing.",
            catalogue: [new CatalogueSourceCitation(42, 105, ["BasePrice"], "Claimed BasePrice.")]));
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "Book 42 edition 105 is missing Narrator.",
            catalogue: [new CatalogueSourceCitation(42, 105, ["Narrator"], "Narrator missing.")]));

        var response = await Runner(chat, provider).RunVerifiedAsync("Check missing fields.");

        Assert.Equal("answered", response.Status);
        Assert.Equal(["Narrator"], Assert.Single(response.Evidence.CatalogueSources).Fields);
    }

    [Fact]
    public async Task Validator_BookLevelAndEditionLevelFieldsCombined_AcceptedForLinkedEdition()
    {
        var provider = CreateCatalogueProvider(
            detailsHandler: bookId => Task.FromResult<object?>(CreateTitleDetails(bookId, 105)));

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", 42)));
        chat.EnqueueText("Audiobook 42 details retrieved.");
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "Title 42 ('Title 42') is missing Narrator on edition 105.",
            catalogue: [new CatalogueSourceCitation(42, 105, ["Title", " Narrator "], "Title and edition 105 field linked.")]));

        var response = await Runner(chat, provider).RunVerifiedAsync("Inspect book 42.");

        Assert.Equal("answered", response.Status);
        Assert.Null(response.Error);

        var ledgerWithoutPlatforms = new EvidenceLedger();
        var noPlatformsDetails = CreateTitleDetails(42, 105) with
        {
            Editions = [CreateTitleDetails(42, 105).Editions[0] with { PlatformSummaries = [] }]
        };
        var noPlatformsJson = JsonSerializer.Serialize(noPlatformsDetails, JsonSerializerOptions.Web);
        Assert.True(ledgerWithoutPlatforms.TryRecordToolOutput(
            "get_title_details",
            new Dictionary<string, object?> { ["bookId"] = 42 },
            noPlatformsDetails,
            noPlatformsJson,
            out _,
            out _));

        var rejectedPlatformCitation = new AgentOutputValidator().Validate(
            SerializeDraft(
                status: "answered",
                answer: "Listed on platform.",
                catalogue: [new CatalogueSourceCitation(42, 105, ["PlatformName"], "Platform cited when summaries were empty.")]),
            ledgerWithoutPlatforms);
        Assert.False(rejectedPlatformCitation.IsValid);
        Assert.Contains("PlatformName", rejectedPlatformCitation.Error);
    }

    [Fact]
    public void Validator_UncorroboratedDocumentCitation_Rejected()
    {
        var ledger = new EvidenceLedger();
        ledger.RecordDocumentHit("doc-other", "doc-other#c1", "Unrelated excerpt.");
        var validator = new AgentOutputValidator();

        var draftJson = SerializeDraft(
            status: "answered",
            answer: "Per the style guide, add a narrator.",
            catalogue: [],
            documents: [new DocumentSourceCitation("doc-guide", "doc-guide#c99", "Guidelines require narrator.")]);

        var result = validator.Validate(draftJson, ledger);

        Assert.False(result.IsValid);
        Assert.Contains("Uncorroborated document link", result.Error);
    }

    [Fact]
    public async Task Validator_MalformedJsonOnAttempt0_RepairedOnAttempt1_Succeeds_AndPreservesAlternatingMessageOrder()
    {
        var provider = CreateCatalogueProvider(
            detailsHandler: bookId => Task.FromResult<object?>(CreateTitleDetails(bookId, 105)));

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", 42)));
        chat.EnqueueText("Book 42 checked.");
        chat.EnqueueText("""{ "status": """); // Malformed JSON on attempt 0
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "Book 42 checked.",
            catalogue: [new CatalogueSourceCitation(42, null, ["Title"], "Title returned.")]));

        var response = await Runner(chat, provider).RunVerifiedAsync("Inspect book 42.");

        Assert.Equal("answered", response.Status);
        Assert.Equal(4, response.Turns);
        Assert.Equal(4, chat.Invocations.Count);

        var repairTurnMessages = chat.Invocations[3];
        Assert.Equal(ChatRole.Assistant, repairTurnMessages[^4].Role); // Stage 1 assistant reply
        Assert.Equal(ChatRole.User, repairTurnMessages[^3].Role);      // Stage2FormattingPrompt
        Assert.Equal(ChatRole.Assistant, repairTurnMessages[^2].Role); // Invalid draft
        Assert.Equal("""{ "status": """, repairTurnMessages[^2].Text);
        Assert.Equal(ChatRole.User, repairTurnMessages[^1].Role);      // Repair prompt
        Assert.Contains("Validation failed:", repairTurnMessages[^1].Text);
    }

    [Fact]
    public void Validator_DuplicateJsonPropertyNames_AndUnmappedProperties_Rejected()
    {
        var ledger = LedgerWithBook42();
        var validator = new AgentOutputValidator();

        var duplicateJson = """
        {
          "status": "answered",
          "status": "could_not_answer",
          "answer": "Book 42 checked.",
          "evidence": {
            "catalogueSources": [{ "bookId": 42, "editionId": 105, "fields": ["Title"], "claim": "Returned." }],
            "documentSources": []
          },
          "limitations": null
        }
        """;
        var dupResult = validator.Validate(duplicateJson, ledger);
        Assert.False(dupResult.IsValid);
        Assert.Contains("duplicate property", dupResult.Error, StringComparison.OrdinalIgnoreCase);

        var unmappedJson = """
        {
          "status": "answered",
          "answer": "Book 42 checked.",
          "evidence": {
            "catalogueSources": [{ "bookId": 42, "editionId": 105, "fields": ["Title"], "claim": "Returned." }],
            "documentSources": []
          },
          "limitations": null,
          "toolsUsed": ["fabricated_admin_tool"]
        }
        """;
        var unmappedResult = validator.Validate(unmappedJson, ledger);
        Assert.False(unmappedResult.IsValid);
        Assert.Contains("strict JSON", unmappedResult.Error, StringComparison.OrdinalIgnoreCase);

        var arrayLimitationsJson = """
        {
          "status": "answered",
          "answer": "Book 42 checked.",
          "evidence": {
            "catalogueSources": [{ "bookId": 42, "editionId": 105, "fields": ["Title"], "claim": "Returned." }],
            "documentSources": []
          },
          "limitations": []
        }
        """;
        var arrayLimitationsResult = validator.Validate(arrayLimitationsJson, ledger);
        Assert.False(arrayLimitationsResult.IsValid);
        Assert.Contains("$.limitations", arrayLimitationsResult.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_DraftByteSizeBoundary_32768BytesAccepted_32769BytesRejected()
    {
        var ledger = LedgerWithBook42();
        var validator = new AgentOutputValidator();

        var cleanPrefix = "{\"status\":\"answered\",\"answer\":\"";
        var cleanSuffix = "\",\"evidence\":{\"catalogueSources\":[{\"bookId\":42,\"editionId\":105,\"fields\":[\"Title\"],\"claim\":\"ok\"}],\"documentSources\":[]},\"limitations\":null}";

        var overhead = Encoding.UTF8.GetByteCount(cleanPrefix) + Encoding.UTF8.GetByteCount(cleanSuffix);
        var exactPadLength = AgentOutputValidator.MaxDraftBytes - overhead;

        var exactDraft = cleanPrefix + new string('a', exactPadLength) + cleanSuffix;
        Assert.Equal(AgentOutputValidator.MaxDraftBytes, Encoding.UTF8.GetByteCount(exactDraft));
        Assert.True(validator.Validate(exactDraft, ledger).IsValid);

        var oneOverAscii = cleanPrefix + new string('a', exactPadLength + 1) + cleanSuffix;
        Assert.Equal(AgentOutputValidator.MaxDraftBytes + 1, Encoding.UTF8.GetByteCount(oneOverAscii));
        var overResult = validator.Validate(oneOverAscii, ledger);
        Assert.False(overResult.IsValid);
        Assert.Contains("32768 bytes", overResult.Error);

        // Multi-byte UTF-8 test: characters count <= 32768 but UTF-8 byte count == 32769 ('\u00e9' is 2 UTF-8 bytes)
        var multiByteDraft = cleanPrefix + new string('\u00e9', exactPadLength / 2) + "aa" + cleanSuffix;
        Assert.True(multiByteDraft.Length < AgentOutputValidator.MaxDraftBytes);
        Assert.True(Encoding.UTF8.GetByteCount(multiByteDraft) > AgentOutputValidator.MaxDraftBytes);
        Assert.False(validator.Validate(multiByteDraft, ledger).IsValid);
    }

    [Fact]
    public void Validator_NullCollectionsAndMalformedCitationFields_Rejected()
    {
        var ledger = LedgerWithBook42();
        var validator = new AgentOutputValidator();

        string[] invalidDrafts =
        [
            """{"status":"answered","answer":"ok","evidence":null,"limitations":null}""",
            """{"status":"answered","answer":"ok","evidence":{"catalogueSources":null,"documentSources":[]},"limitations":null}""",
            """{"status":"answered","answer":"ok","evidence":{"catalogueSources":[{"bookId":0,"editionId":null,"fields":["Title"],"claim":"x"}],"documentSources":[]},"limitations":null}""",
            """{"status":"answered","answer":"ok","evidence":{"catalogueSources":[{"bookId":42,"editionId":-1,"fields":["Title"],"claim":"x"}],"documentSources":[]},"limitations":null}""",
            """{"status":"answered","answer":"ok","evidence":{"catalogueSources":[{"bookId":42,"editionId":null,"fields":[" "],"claim":"x"}],"documentSources":[]},"limitations":null}""",
            """{"status":"answered","answer":"ok","evidence":{"catalogueSources":[{"bookId":42,"editionId":null,"fields":["Title"],"claim":""}],"documentSources":[]},"limitations":null}""",
            """{"status":"partial","answer":"ok","evidence":{"catalogueSources":[{"bookId":42,"editionId":null,"fields":["Title"],"claim":"x"}],"documentSources":[]},"limitations":null}"""
        ];

        foreach (var raw in invalidDrafts)
        {
            Assert.False(validator.Validate(raw, ledger).IsValid, $"Expected rejection for: {raw}");
        }
    }

    [Fact]
    public async Task Validator_UnrelatedLimitationsWhenPaginationTruncated_Rejected_UntilTruncationDisclosed()
    {
        var provider = CreateCatalogueProvider(
            findHandler: (_, _, page, pageSize) => Task.FromResult<object?>(new MissingMetadataPageDto(
                page, pageSize, 25, true,
                [new MissingTitleItemDto(42, "Truncated Item", [], [new MissingEditionFieldsDto(105, "Ebook", ["FileSize"])])])));

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("page", 1), ("pageSize", 1)));
        chat.EnqueueText("First page loaded; more exist.");
        // Attempt 0 gives unrelated non-empty limitation text
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "Book 42 edition 105 is missing FileSize.",
            catalogue: [new CatalogueSourceCitation(42, 105, ["FileSize"], "Missing FileSize.")],
            limitations: "Sales tax figures are excluded."));
        // Attempt 1 properly discloses pagination truncation
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "Book 42 edition 105 is missing FileSize.",
            catalogue: [new CatalogueSourceCitation(42, 105, ["FileSize"], "Missing FileSize.")],
            limitations: "Results were paginated and truncated after page 1; more items exist."));

        var response = await Runner(chat, provider).RunVerifiedAsync("Find missing metadata.");

        Assert.Equal("answered", response.Status);
        Assert.Equal(4, response.Turns);
        Assert.Contains("truncated", response.Limitations, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hasMore=true", response.Limitations, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pagination_OutOfOrderPage2First_OrMidQueryPageSizeChange_HaltsWithPaginationSequenceError()
    {
        var invoked = false;
        var outOfOrderProvider = CreateCatalogueProvider(
            findHandler: (_, _, page, pageSize) =>
            {
                invoked = true;
                return Task.FromResult<object?>(new MissingMetadataPageDto(page, pageSize, 0, false, []));
            });

        var chatA = new ScriptedChatClient();
        chatA.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("page", 2), ("pageSize", 10)));
        var resA = await Runner(chatA, outOfOrderProvider).RunVerifiedAsync("Start on page 2.");

        Assert.False(invoked);
        Assert.Equal("could_not_answer", resA.Status);
        Assert.StartsWith("PAGINATION_SEQUENCE_ERROR:", resA.Error);

        var midChangeProvider = CreateCatalogueProvider(
            findHandler: (_, _, page, pageSize) => Task.FromResult<object?>(new MissingMetadataPageDto(
                page, pageSize, 25, true,
                [new MissingTitleItemDto(42, "Item", [], [new MissingEditionFieldsDto(105, "Ebook", ["FileSize"])])])));

        var chatB = new ScriptedChatClient();
        chatB.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("page", 1), ("pageSize", 10)));
        chatB.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("page", 2), ("pageSize", 20)));
        var resB = await Runner(chatB, midChangeProvider).RunVerifiedAsync("Change pageSize on page 2.");

        Assert.Equal("could_not_answer", resB.Status);
        Assert.StartsWith("PAGINATION_SEQUENCE_ERROR:", resB.Error);
    }

    [Fact]
    public async Task Pagination_InconsistentReturnedPageOrTotalCount_HaltsWithEvidenceIndexingError()
    {
        // Case A: Tool returns page=2 when page=1 was requested
        var wrongPageProvider = CreateCatalogueProvider(
            findHandler: (_, _, _, pageSize) => Task.FromResult<object?>(new MissingMetadataPageDto(
                2, pageSize, 25, true,
                [new MissingTitleItemDto(42, "Item", [], [new MissingEditionFieldsDto(105, "Ebook", ["FileSize"])])])));

        var chatA = new ScriptedChatClient();
        chatA.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("page", 1), ("pageSize", 10)));
        var resA = await Runner(chatA, wrongPageProvider).RunVerifiedAsync("Check page mismatch.");

        Assert.StartsWith("EVIDENCE_INDEXING_ERROR:", resA.Error);

        // Case B: TotalCount=50, hasMore=false with only 1 item on page 1
        var badMathProvider = CreateCatalogueProvider(
            findHandler: (_, _, page, pageSize) => Task.FromResult<object?>(new MissingMetadataPageDto(
                page, pageSize, 50, false,
                [new MissingTitleItemDto(42, "Item", [], [new MissingEditionFieldsDto(105, "Ebook", ["FileSize"])])])));

        var chatB = new ScriptedChatClient();
        chatB.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("page", 1), ("pageSize", 10)));
        var resB = await Runner(chatB, badMathProvider).RunVerifiedAsync("Check totalCount math.");

        Assert.StartsWith("EVIDENCE_INDEXING_ERROR:", resB.Error);
    }

    [Fact]
    public async Task EvidenceLedger_UnparseableOrMalformedToolPayload_HaltsWithEvidenceIndexingError()
    {
        var malformedProvider = CreateCatalogueProvider(
            findHandler: (_, _, _, _) => Task.FromResult<object?>(new { unexpected = true }));

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata"));

        var response = await Runner(chat, malformedProvider).RunVerifiedAsync("Check malformed payload.");

        Assert.Equal("could_not_answer", response.Status);
        Assert.StartsWith("EVIDENCE_INDEXING_ERROR:", response.Error);

        var wrongBookProvider = CreateCatalogueProvider(
            detailsHandler: _ => Task.FromResult<object?>(CreateTitleDetails(99, 105)));
        var chatBookMismatch = new ScriptedChatClient();
        chatBookMismatch.EnqueueFunctionCalls(Call("get_title_details", ("bookId", 42)));

        var mismatchResponse = await Runner(chatBookMismatch, wrongBookProvider).RunVerifiedAsync("Check book mismatch.");

        Assert.Equal("could_not_answer", mismatchResponse.Status);
        Assert.StartsWith("EVIDENCE_INDEXING_ERROR:", mismatchResponse.Error);
    }

    [Fact]
    public async Task EvidenceLedger_RealMcpCallToolResultJsonElement_UnwrapsStructuredContentCorrectly()
    {
        var callToolResultJson = JsonSerializer.SerializeToElement(new
        {
            isError = false,
            structuredContent = new MissingMetadataPageDto(
                1, 20, 1, false,
                [new MissingTitleItemDto(42, "Wrapped Title", ["Description"], [new MissingEditionFieldsDto(105, "Ebook", ["FileSize"])])]),
            content = new[] { new { type = "text", text = "{}" } }
        }, JsonSerializerOptions.Web);

        var provider = CreateCatalogueProvider(
            findHandler: (_, _, _, _) => Task.FromResult<object?>(callToolResultJson));

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata"));
        chat.EnqueueText("Found Wrapped Title missing Description and FileSize.");
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "Wrapped Title (bookId 42, edition 105) is missing Description and FileSize.",
            catalogue: [new CatalogueSourceCitation(42, 105, ["Description", "FileSize"], "Indexed from CallToolResult.")]));

        var response = await Runner(chat, provider).RunVerifiedAsync("Check real CallToolResult envelope.");

        Assert.Equal("answered", response.Status);
        Assert.Null(response.Error);
        Assert.Equal(42, Assert.Single(response.Evidence.CatalogueSources).BookId);
    }

    [Fact]
    public async Task Stage2_PerCallTimeoutOnStructuredTurn_HaltsWithTimeoutErrorCode()
    {
        var chat = new ScriptedChatClient();
        chat.EnqueueText("No tools needed; initial draft text.");
        chat.Enqueue(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "unreachable"));
        });

        var response = await Runner(chat, CreateCatalogueProvider(), perCallMs: 25, overallMs: 500)
            .RunVerifiedAsync("Trigger Stage 2 timeout.");

        Assert.Equal("could_not_answer", response.Status);
        Assert.StartsWith("TIMEOUT:", response.Error);
    }

    [Fact]
    public async Task Stage2_ValidationRetriesExhausted_FailsWithApprovedErrorCodeAndPreservesHostToolsUsed()
    {
        var provider = CreateCatalogueProvider(
            detailsHandler: bookId => Task.FromResult<object?>(CreateTitleDetails(bookId, 105)));

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", 42)));
        chat.EnqueueText("Book 42 fetched.");
        chat.EnqueueText("not json 0");
        chat.EnqueueText("not json 1");
        chat.EnqueueText("not json 2");

        var response = await Runner(chat, provider, maxValidationRetries: 2).RunVerifiedAsync("Inspect book 42.");

        Assert.Equal("could_not_answer", response.Status);
        Assert.StartsWith("VALIDATION_RETRIES_EXHAUSTED:", response.Error);
        Assert.Equal(5, response.Turns); // 2 Stage 1 turns + 3 Stage 2 attempts
        Assert.Equal("get_title_details", Assert.Single(response.ToolsUsed).ToolName);
    }

    [Fact]
    public async Task Stage2_RepairRetryHitsMaxModelTurnsCeiling_HaltsWithTurnLimitExceeded()
    {
        var provider = CreateCatalogueProvider(
            detailsHandler: bookId => Task.FromResult<object?>(CreateTitleDetails(bookId, 105)));

        var chat = new ScriptedChatClient();
        for (var i = 1; i <= 4; i++)
            chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", i))); // Turns 1..4
        chat.EnqueueText("All 4 books fetched."); // Turn 5 ends Stage 1
        chat.EnqueueText("not json");             // Turn 6 (Stage 2 attempt 0) fails; attempt 1 must not invoke client

        var response = await Runner(chat, provider, maxTurns: 6, maxValidationRetries: 2)
            .RunVerifiedAsync("Inspect 4 books.");

        Assert.Equal("could_not_answer", response.Status);
        Assert.StartsWith("TURN_LIMIT_EXCEEDED:", response.Error);
        Assert.Equal(6, response.Turns);
        Assert.Equal(6, chat.Invocations.Count);

        var turn5ToolCalls = 0;
        var countingProvider = CreateCatalogueProvider(
            detailsHandler: bookId =>
            {
                turn5ToolCalls++;
                return Task.FromResult<object?>(CreateTitleDetails(bookId, 105));
            });

        var stage1ExhaustedChat = new ScriptedChatClient();
        for (var i = 1; i <= 5; i++)
            stage1ExhaustedChat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", i))); // Turn 5 still calls tools

        var stage1ExhaustedResponse = await Runner(stage1ExhaustedChat, countingProvider, maxTurns: 6, maxValidationRetries: 2)
            .RunVerifiedAsync("Run 5 tool turns without stopping for Stage 2.");

        Assert.Equal("could_not_answer", stage1ExhaustedResponse.Status);
        Assert.StartsWith("TURN_LIMIT_EXCEEDED:", stage1ExhaustedResponse.Error);
        Assert.Equal(5, stage1ExhaustedResponse.Turns);
        Assert.Equal(5, stage1ExhaustedChat.Invocations.Count);
        Assert.Equal(4, turn5ToolCalls);
    }

    [Fact]
    public async Task FailureEnvelope_ConstrainedToApprovedErrorCodes_NeverLeaksInternalDiagnosticStrings()
    {
        const string sensitiveInternalSecret = "Server=internal-pg-01;Password=SecretPassword123";
        var failingProvider = CreateCatalogueProvider(
            detailsHandler: _ => throw new InvalidOperationException(sensitiveInternalSecret));

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", 42)));

        var response = await Runner(chat, failingProvider).RunVerifiedAsync("Inspect book 42.");

        Assert.Equal("could_not_answer", response.Status);
        Assert.StartsWith("TOOL_ERROR:", response.Error);
        Assert.DoesNotContain("internal-pg-01", response.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("SecretPassword123", response.Error, StringComparison.Ordinal);

        foreach (var code in Enum.GetValues<AgentErrorCode>())
        {
            var envelope = AgentQueryResponse.Failure(code, [], 1, 0);
            Assert.StartsWith($"{code}:", envelope.Error);
        }
    }

    private static AgentLoopRunner Runner(
        ScriptedChatClient chat,
        IMcpToolProvider provider,
        int maxTurns = 6,
        int maxValidationRetries = 2,
        int perCallMs = 1_000,
        int overallMs = 5_000) =>
        new(
            chat,
            provider,
            Options.Create(new AgentLoopOptions
            {
                MaxModelTurns = maxTurns,
                MaxValidationRetries = maxValidationRetries,
                PerCallTimeout = TimeSpan.FromMilliseconds(perCallMs),
                OverallTimeout = TimeSpan.FromMilliseconds(overallMs)
            }),
            NullLogger<AgentLoopRunner>.Instance,
            new AgentOutputValidator());

    private static IMcpToolProvider CreateCatalogueProvider(
        Func<string?, string?, int, int, Task<object?>>? findHandler = null,
        Func<int, Task<object?>>? detailsHandler = null)
    {
        FindTitlesDelegate findDel = (string? format = null, string? missingField = null, int page = 1, int pageSize = 20) =>
            (findHandler ?? ((_, _, p, ps) => Task.FromResult<object?>(new MissingMetadataPageDto(p, ps, 0, false, []))))(format, missingField, page, pageSize);

        GetDetailsDelegate detailsDel = (int bookId = 1) =>
            (detailsHandler ?? (id => Task.FromResult<object?>(CreateTitleDetails(id, 100))))(bookId);

        var marshal = new AIFunctionFactoryOptions { MarshalResult = (result, _, _) => ValueTask.FromResult(result) };
        var findTool = AIFunctionFactory.Create(findDel, new AIFunctionFactoryOptions
        {
            Name = "find_titles_missing_metadata",
            Description = "find_titles_missing_metadata",
            MarshalResult = marshal.MarshalResult
        });
        var detailsTool = AIFunctionFactory.Create(detailsDel, new AIFunctionFactoryOptions
        {
            Name = "get_title_details",
            Description = "get_title_details",
            MarshalResult = marshal.MarshalResult
        });

        return new StubToolProvider(findTool, detailsTool);
    }

    private delegate Task<object?> FindTitlesDelegate(string? format = null, string? missingField = null, int page = 1, int pageSize = 20);
    private delegate Task<object?> GetDetailsDelegate(int bookId = 1);

    private static (string Name, IDictionary<string, object?> Arguments) Call(string name, params (string Key, object? Value)[] values) =>
        (name, values.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal));

    private static TitleDetailsDto CreateTitleDetails(int bookId, int editionId) =>
        new(
            BookId: bookId,
            Title: $"Title {bookId}",
            Author: "Author Alpha",
            SeriesName: null,
            SeriesNumber: null,
            Genre: "Sci-Fi",
            Description: "Sample description.",
            CoverImageUrl: "https://example.test/cover.jpg",
            CreatedAt: DateTime.UnixEpoch,
            UpdatedAt: DateTime.UnixEpoch,
            MissingBookFields: [],
            Editions:
            [
                new EditionDetailsDto(
                    EditionId: editionId,
                    Format: "Audiobook",
                    ISBN: null,
                    PublicationDate: DateTime.UnixEpoch,
                    BasePrice: 19.99m,
                    PageCount: null,
                    Dimensions: null,
                    FileSize: null,
                    Narrator: null,
                    Duration: null,
                    MissingFields: ["Narrator", "Duration"],
                    PlatformSummaries: [new PlatformSummaryDto(1, "Kindle Direct", false, null, "B0AUDIO1")])
            ]);

    private static EvidenceLedger LedgerWithBook42()
    {
        var ledger = new EvidenceLedger();
        var details = CreateTitleDetails(42, 105);
        var json = JsonSerializer.Serialize(details, JsonSerializerOptions.Web);
        Assert.True(ledger.TryRecordToolOutput("get_title_details", new Dictionary<string, object?> { ["bookId"] = 42 }, details, json, out _, out _));
        return ledger;
    }

    private static string SerializeDraft(
        string status,
        string answer,
        IReadOnlyList<CatalogueSourceCitation> catalogue,
        IReadOnlyList<DocumentSourceCitation>? documents = null,
        string? limitations = null) =>
        JsonSerializer.Serialize(
            new AgentModelDraft(status, answer, new AgentEvidenceDraft(catalogue, documents ?? []), limitations),
            JsonSerializerOptions.Web);
}
