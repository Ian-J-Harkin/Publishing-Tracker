using PublishingTracker.Agent.Contracts;
using PublishingTracker.Evals.Evaluators;
using PublishingTracker.Evals.Models;

namespace PublishingTracker.Evals;

public sealed class ProvenanceScorerTests
{
    private static readonly EvalScenario RagScenario = new(
        "rag-scenario",
        "desc",
        "prompt",
        ExpectedTools: ["search_publishing_guidelines"],
        ForbiddenTools: [],
        ExpectedDocumentIds: ["02-print-and-ebook-format-specs"],
        ExpectAnswered: true);

    [Fact]
    public void Evaluate_CitationWithinExpectedDocumentSet_ReturnsNoFailures()
    {
        var response = AnsweredWithDocumentCitation("02-print-and-ebook-format-specs", "c1", "Ebook covers must be at least 1600x2400.");
        Assert.Empty(ProvenanceScorer.Evaluate(RagScenario, response));
    }

    [Fact]
    public void Evaluate_CitationOutsideExpectedDocumentSet_FlagsHallucinatedProvenance()
    {
        var response = AnsweredWithDocumentCitation("04-platform-publishing-rules", "c1", "Unrelated claim.");
        var failures = ProvenanceScorer.Evaluate(RagScenario, response);
        Assert.Contains(failures, f => f.Contains("hallucinated provenance", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_CitationMissingChunkIdOrClaim_FlagsStructuralIncompleteness()
    {
        var response = AnsweredWithDocumentCitation("02-print-and-ebook-format-specs", chunkId: "", claim: "");
        var failures = ProvenanceScorer.Evaluate(RagScenario, response);
        Assert.Contains(failures, f => f.Contains("no chunk id", StringComparison.Ordinal));
        Assert.Contains(failures, f => f.Contains("empty claim", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_CatalogueCitationMissingFieldsOrClaim_FlagsStructuralIncompleteness()
    {
        var scenario = new EvalScenario("c", "d", "p", [], [], [], ExpectAnswered: true);
        var response = AgentQueryResponse.Answered(
            new AgentModelDraft("answered", "ok", new AgentEvidenceDraft([new CatalogueSourceCitation(42, null, [], "")], []), null),
            hasPaginationTruncation: false,
            toolsUsed: [],
            turns: 1,
            totalToolCalls: 0);

        var failures = ProvenanceScorer.Evaluate(scenario, response);
        Assert.Contains(failures, f => f.Contains("no cited fields", StringComparison.Ordinal));
        Assert.Contains(failures, f => f.Contains("empty claim", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_UnanswerableScenarioWithCitations_Fails()
    {
        var unanswerable = new EvalScenario("c", "d", "p", [], [], [], ExpectAnswered: false);
        var response = AnsweredWithDocumentCitation("doc", "c1", "claim");

        var failures = ProvenanceScorer.Evaluate(unanswerable, response);
        Assert.Contains(failures, f => f.Contains("expected no answer", StringComparison.Ordinal));
    }

    private static AgentQueryResponse AnsweredWithDocumentCitation(string documentId, string chunkId, string claim) =>
        AgentQueryResponse.Answered(
            new AgentModelDraft("answered", "ok", new AgentEvidenceDraft([], [new DocumentSourceCitation(documentId, chunkId, claim)]), null),
            hasPaginationTruncation: false,
            toolsUsed: [],
            turns: 1,
            totalToolCalls: 1);
}
