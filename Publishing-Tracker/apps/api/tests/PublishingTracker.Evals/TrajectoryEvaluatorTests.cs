using PublishingTracker.Agent.Contracts;
using PublishingTracker.Evals.Evaluators;
using PublishingTracker.Evals.Models;

namespace PublishingTracker.Evals;

public sealed class TrajectoryEvaluatorTests
{
    private static readonly EvalScenario Scenario = new(
        "test-scenario",
        "desc",
        "prompt",
        ExpectedTools: ["find_titles_missing_metadata"],
        ForbiddenTools: ["search_publishing_guidelines"],
        ExpectedDocumentIds: [],
        ExpectAnswered: true);

    [Fact]
    public void Evaluate_MatchingExpectations_ReturnsNoFailures()
    {
        var response = Answered(["find_titles_missing_metadata"], turns: 2, totalToolCalls: 1);
        Assert.Empty(TrajectoryEvaluator.Evaluate(Scenario, response));
    }

    [Fact]
    public void Evaluate_MissingExpectedTool_Fails()
    {
        var response = Answered([], turns: 2, totalToolCalls: 0);
        var failures = TrajectoryEvaluator.Evaluate(Scenario, response);
        Assert.Contains(failures, f => f.Contains("find_titles_missing_metadata", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_ForbiddenToolInvoked_Fails()
    {
        var response = Answered(["find_titles_missing_metadata", "search_publishing_guidelines"], turns: 3, totalToolCalls: 2);
        var failures = TrajectoryEvaluator.Evaluate(Scenario, response);
        Assert.Contains(failures, f => f.Contains("Forbidden tool", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_StatusMismatchWithExpectAnswered_Fails()
    {
        var response = AgentQueryResponse.Failure(AgentErrorCode.TOOL_ERROR, [], 1, 0);
        var failures = TrajectoryEvaluator.Evaluate(Scenario, response);
        Assert.Contains(failures, f => f.Contains("ExpectAnswered", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_GoldenTrajectoryMismatch_FlagsSequenceTurnsAndToolCallDrift()
    {
        var response = Answered(["find_titles_missing_metadata"], turns: 4, totalToolCalls: 1);
        var golden = new GoldenTrajectory(Scenario.Id, ["find_titles_missing_metadata", "get_title_details"], ExpectedTurns: 2, ExpectedTotalToolCalls: 2);

        var failures = TrajectoryEvaluator.Evaluate(Scenario, response, golden);

        Assert.Contains(failures, f => f.Contains("Tool sequence", StringComparison.Ordinal));
        Assert.Contains(failures, f => f.Contains("Turn count", StringComparison.Ordinal));
        Assert.Contains(failures, f => f.Contains("Total tool calls", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_GoldenTrajectoryMatch_ReturnsNoFailures()
    {
        var response = Answered(["find_titles_missing_metadata"], turns: 2, totalToolCalls: 1);
        var golden = new GoldenTrajectory(Scenario.Id, ["find_titles_missing_metadata"], ExpectedTurns: 2, ExpectedTotalToolCalls: 1);

        Assert.Empty(TrajectoryEvaluator.Evaluate(Scenario, response, golden));
    }

    private static AgentQueryResponse Answered(IReadOnlyList<string> toolNames, int turns, int totalToolCalls) =>
        AgentQueryResponse.Answered(
            new AgentModelDraft("answered", "ok", new AgentEvidenceDraft([], []), null),
            hasPaginationTruncation: false,
            toolsUsed: toolNames.Select(name => new ToolExecutionRecord(name, 1, 10, true)).ToArray(),
            turns: turns,
            totalToolCalls: totalToolCalls);
}
