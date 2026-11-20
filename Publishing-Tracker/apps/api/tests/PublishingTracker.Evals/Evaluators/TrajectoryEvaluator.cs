using PublishingTracker.Agent.Contracts;
using PublishingTracker.Evals.Models;

namespace PublishingTracker.Evals.Evaluators;

/// <summary>
/// Scores a completed <see cref="AgentQueryResponse"/> against the trajectory expectations
/// declared by an <see cref="EvalScenario"/> (and, for golden-baseline comparisons, an
/// exact <see cref="GoldenTrajectory"/>): tool selection, forbidden-tool avoidance, turn
/// economy, and answered/could_not_answer status.
/// </summary>
public static class TrajectoryEvaluator
{
    public static IReadOnlyList<string> Evaluate(EvalScenario scenario, AgentQueryResponse response, GoldenTrajectory? golden = null)
    {
        var failures = new List<string>();
        var usedToolNames = response.ToolsUsed.Select(t => t.ToolName).ToArray();

        var answered = string.Equals(response.Status, "answered", StringComparison.Ordinal) ||
                       string.Equals(response.Status, "partial", StringComparison.Ordinal);
        if (answered != scenario.ExpectAnswered)
        {
            failures.Add($"Expected ExpectAnswered={scenario.ExpectAnswered} but status was '{response.Status}'.");
        }

        foreach (var expectedTool in scenario.ExpectedTools)
        {
            if (!usedToolNames.Contains(expectedTool, StringComparer.Ordinal))
            {
                failures.Add($"Expected tool '{expectedTool}' was not invoked.");
            }
        }

        foreach (var forbiddenTool in scenario.ForbiddenTools)
        {
            if (usedToolNames.Contains(forbiddenTool, StringComparer.Ordinal))
            {
                failures.Add($"Forbidden tool '{forbiddenTool}' was invoked.");
            }
        }

        if (response.ToolsUsed.Any(t => !t.Succeeded) && scenario.ExpectAnswered)
        {
            failures.Add("A tool call recorded as failed while the scenario expected a successful answer.");
        }

        if (golden is not null)
        {
            if (golden.ScenarioId != scenario.Id)
            {
                failures.Add($"Golden trajectory scenario id '{golden.ScenarioId}' does not match scenario '{scenario.Id}'.");
            }

            if (!usedToolNames.SequenceEqual(golden.ExpectedToolSequence, StringComparer.Ordinal))
            {
                failures.Add($"Tool sequence [{string.Join(", ", usedToolNames)}] did not match golden sequence [{string.Join(", ", golden.ExpectedToolSequence)}].");
            }

            if (response.Turns != golden.ExpectedTurns)
            {
                failures.Add($"Turn count {response.Turns} did not match golden turn count {golden.ExpectedTurns}.");
            }

            if (response.TotalToolCalls != golden.ExpectedTotalToolCalls)
            {
                failures.Add($"Total tool calls {response.TotalToolCalls} did not match golden total {golden.ExpectedTotalToolCalls}.");
            }
        }

        return failures;
    }
}
