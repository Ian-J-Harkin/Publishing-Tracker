using PublishingTracker.Agent.Contracts;
using PublishingTracker.Evals.Models;

namespace PublishingTracker.Evals.Evaluators;

/// <summary>
/// Validates an agent execution response against the checked-in baseline scorecard.
/// Flags regressions in turn economy, tool quotas, forbidden tools, or retrieval quality.
/// </summary>
public static class BaselineEnforcer
{
    public static IReadOnlyList<string> Validate(
        ScenarioBaseline baseline,
        AgentQueryResponse response,
        IReadOnlyList<string>? retrievedDocumentIds = null)
    {
        var violations = new List<string>();

        if (response.Turns > baseline.MaxModelTurns)
        {
            violations.Add($"Turns ({response.Turns}) exceeded baseline max ({baseline.MaxModelTurns}).");
        }

        if (response.TotalToolCalls > baseline.MaxTotalToolCalls)
        {
            violations.Add($"Tool calls ({response.TotalToolCalls}) exceeded baseline max ({baseline.MaxTotalToolCalls}).");
        }

        var called = response.ToolsUsed.Select(c => c.ToolName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (baseline.ForbiddenTools != null)
        {
            foreach (var forbidden in baseline.ForbiddenTools)
            {
                if (called.Contains(forbidden))
                {
                    violations.Add($"Forbidden tool '{forbidden}' was called.");
                }
            }
        }

        if (baseline.ExpectedTools.Count > 0)
        {
            var matched = baseline.ExpectedTools.Count(t => called.Contains(t));
            var ratio = (double)matched / baseline.ExpectedTools.Count;
            if (ratio < baseline.MinExpectedToolsCalledRatio)
            {
                violations.Add($"Expected tools ratio ({ratio:P0}) fell below baseline minimum ({baseline.MinExpectedToolsCalledRatio:P0}).");
            }
        }

        if (baseline.RejectionExpected == true && response.Status == "answered")
        {
            violations.Add("Expected query to be rejected or unanswerable, but agent reported 'answered'.");
        }

        if (baseline.ExpectedDocumentIds != null && baseline.ExpectedDocumentIds.Count > 0 && retrievedDocumentIds != null)
        {
            var topK = baseline.TopK ?? 3;
            var topHits = retrievedDocumentIds.Take(topK).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var retrievedExpected = baseline.ExpectedDocumentIds.Count(id => topHits.Contains(id));
            var recall = (double)retrievedExpected / baseline.ExpectedDocumentIds.Count;
            var targetRecall = baseline.MinRecallAtK ?? 1.0;
            if (recall < targetRecall)
            {
                violations.Add($"Recall@{topK} ({recall:P0}) fell below baseline target ({targetRecall:P0}).");
            }
        }

        return violations;
    }
}
