using PublishingTracker.Agent.Contracts;
using PublishingTracker.Evals.Models;

namespace PublishingTracker.Evals.Evaluators;

/// <summary>
/// Scores a completed <see cref="AgentQueryResponse"/> against ADR-007's Structural
/// Provenance Rate: every citation must be structurally complete, and no citation may
/// reference a document outside the scenario's known corpus (a proxy for hallucinated
/// provenance when evaluating against a fixed golden dataset).
/// </summary>
public static class ProvenanceScorer
{
    public static IReadOnlyList<string> Evaluate(EvalScenario scenario, AgentQueryResponse response)
    {
        var failures = new List<string>();

        foreach (var citation in response.Evidence.CatalogueSources)
        {
            if (citation.Fields.Count == 0)
                failures.Add($"Catalogue citation for book {citation.BookId} has no cited fields.");
            if (string.IsNullOrWhiteSpace(citation.Claim))
                failures.Add($"Catalogue citation for book {citation.BookId} has an empty claim.");
        }

        foreach (var citation in response.Evidence.DocumentSources)
        {
            if (string.IsNullOrWhiteSpace(citation.ChunkId))
                failures.Add($"Document citation for '{citation.DocumentId}' has no chunk id.");
            if (string.IsNullOrWhiteSpace(citation.Claim))
                failures.Add($"Document citation for '{citation.DocumentId}' has an empty claim.");

            if (scenario.ExpectedDocumentIds.Count > 0 &&
                !scenario.ExpectedDocumentIds.Contains(citation.DocumentId, StringComparer.Ordinal))
            {
                failures.Add(
                    $"Document citation references '{citation.DocumentId}', which is outside the scenario's " +
                    "expected document set (possible hallucinated provenance).");
            }
        }

        if (!scenario.ExpectAnswered &&
            (response.Evidence.CatalogueSources.Count > 0 || response.Evidence.DocumentSources.Count > 0))
        {
            failures.Add("Scenario expected no answer, but citations were still produced.");
        }

        return failures;
    }
}
