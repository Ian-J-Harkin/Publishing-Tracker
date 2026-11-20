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
using PublishingTracker.Evals.Evaluators;
using PublishingTracker.Rag.Contracts;
using PublishingTracker.Rag.Tools;

namespace PublishingTracker.Evals;

/// <summary>
/// Step 5.2 deliverable: proves <see cref="TrajectoryEvaluator"/> and <see cref="ProvenanceScorer"/>
/// correctly pass/fail real, deterministic <see cref="AgentLoopRunner"/> runs (ScriptedChatClient +
/// StubToolProvider, zero network/API spend) scored against the Phase 5 golden dataset.
/// </summary>
public sealed class GoldenScenarioEndToEndTests
{
    [Fact]
    public async Task CatalogueScenario_ScriptedRunMatchingGoldenExpectations_PassesBothEvaluators()
    {
        var scenario = Scenario("catalogue-missing-audiobook-metadata");

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

        var response = await Runner(chat, provider).RunVerifiedAsync(scenario.Prompt);

        Assert.Empty(TrajectoryEvaluator.Evaluate(scenario, response));
        Assert.Empty(ProvenanceScorer.Evaluate(scenario, response));
    }

    [Fact]
    public async Task RagScenario_CitationWithinLockedCorpus_PassesBothEvaluators()
    {
        var scenario = Scenario("rag-ebook-formatting-guidelines");

        var provider = new StubToolProvider(RagToolReturning("02-print-and-ebook-format-specs", "02-print-and-ebook-format-specs#c3", "Ebook covers must be at least 1600x2400 pixels."));

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call(RagSearchToolFactory.ToolName, ("query", "ebook file size and cover guidelines")));
        chat.EnqueueText("Found the formatting guideline chunk.");
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "Ebook covers must be at least 1600x2400 pixels, and file sizes should stay under the platform cap.",
            catalogue: [],
            documents: [new DocumentSourceCitation("02-print-and-ebook-format-specs", "02-print-and-ebook-format-specs#c3", "Ebook covers must be at least 1600x2400 pixels.")]));

        var response = await Runner(chat, provider).RunVerifiedAsync(scenario.Prompt);

        Assert.Empty(TrajectoryEvaluator.Evaluate(scenario, response));
        Assert.Empty(ProvenanceScorer.Evaluate(scenario, response));
    }

    [Fact]
    public async Task RagScenario_CitationOutsideLockedCorpus_FailsProvenanceScorer_EvenThoughAgentLoopAccepts()
    {
        var scenario = Scenario("rag-ebook-formatting-guidelines");

        // The tool legitimately returns a hit from an unrelated document; the model cites it anyway.
        // The agent loop itself only enforces *structural* linkage (the citation matches a real ledger
        // hit), so this passes AgentOutputValidator — it is the golden-dataset ProvenanceScorer's job
        // to catch that the cited document is outside this scenario's known corpus.
        var provider = new StubToolProvider(RagToolReturning("04-platform-publishing-rules", "04-platform-publishing-rules#c1", "Unrelated platform rule."));

        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call(RagSearchToolFactory.ToolName, ("query", "ebook file size and cover guidelines")));
        chat.EnqueueText("Found a chunk.");
        chat.EnqueueText(SerializeDraft(
            status: "answered",
            answer: "Ebook covers must follow platform distribution rules.",
            catalogue: [],
            documents: [new DocumentSourceCitation("04-platform-publishing-rules", "04-platform-publishing-rules#c1", "Unrelated platform rule.")]));

        var response = await Runner(chat, provider).RunVerifiedAsync(scenario.Prompt);

        Assert.Empty(TrajectoryEvaluator.Evaluate(scenario, response));
        var provenanceFailures = ProvenanceScorer.Evaluate(scenario, response);
        Assert.Contains(provenanceFailures, f => f.Contains("hallucinated provenance", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnanswerableScenario_ScriptedRunDeclines_PassesBothEvaluators()
    {
        var scenario = Scenario("unanswerable-historical-pricing");

        var chat = new ScriptedChatClient();
        chat.EnqueueText("No price-history tool exists.");
        chat.EnqueueText(SerializeDraft(
            status: "could_not_answer",
            answer: "Historical pricing on a past date is not available in the read-only catalogue tools.",
            catalogue: []));

        var response = await Runner(chat, CreateCatalogueProvider()).RunVerifiedAsync(scenario.Prompt);

        Assert.Empty(TrajectoryEvaluator.Evaluate(scenario, response));
        Assert.Empty(ProvenanceScorer.Evaluate(scenario, response));
    }

    private static Models.EvalScenario Scenario(string id) =>
        GoldenDataset.Scenarios.Single(s => s.Id == id);

    private static AIFunction RagToolReturning(string documentId, string chunkId, string excerpt) =>
        AIFunctionFactory.Create(
            (string query, int topK = 5) => Task.FromResult(new RagSearchResultDto(query, topK, [new RagChunkHitDto(documentId, chunkId, "Title", excerpt, 0.01)])),
            new AIFunctionFactoryOptions { Name = RagSearchToolFactory.ToolName });

    private static AgentLoopRunner Runner(ScriptedChatClient chat, IMcpToolProvider provider) =>
        new(
            chat,
            provider,
            Options.Create(new AgentLoopOptions
            {
                PerCallTimeout = TimeSpan.FromSeconds(5),
                OverallTimeout = TimeSpan.FromSeconds(15)
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

    private static string SerializeDraft(
        string status,
        string answer,
        IReadOnlyList<CatalogueSourceCitation> catalogue,
        IReadOnlyList<DocumentSourceCitation>? documents = null,
        string? limitations = null) =>
        System.Text.Json.JsonSerializer.Serialize(
            new AgentModelDraft(status, answer, new AgentEvidenceDraft(catalogue, documents ?? []), limitations),
            System.Text.Json.JsonSerializerOptions.Web);
}
