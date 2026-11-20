namespace PublishingTracker.Evals.Models;

/// <summary>
/// The expected tool-call trajectory and turn economy for a scripted, deterministic
/// golden-dataset run. Used by <see cref="PublishingTracker.Evals.Evaluators.TrajectoryEvaluator"/>
/// to detect regressions in tool selection or turn count against a checked-in baseline.
/// </summary>
public sealed record GoldenTrajectory(
    string ScenarioId,
    IReadOnlyList<string> ExpectedToolSequence,
    int ExpectedTurns,
    int ExpectedTotalToolCalls);
