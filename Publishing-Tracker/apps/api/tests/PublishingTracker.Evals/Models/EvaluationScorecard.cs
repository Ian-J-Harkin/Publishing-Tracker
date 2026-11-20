namespace PublishingTracker.Evals.Models;

/// <summary>
/// Aggregate scoring result for one scenario run, combining trajectory and provenance
/// verdicts. A scenario only passes the overall gate when every category passes with no
/// recorded failures.
/// </summary>
public sealed record EvaluationScorecard(
    string ScenarioId,
    bool TrajectoryPassed,
    bool ProvenancePassed,
    IReadOnlyList<string> Failures)
{
    public bool Passed => TrajectoryPassed && ProvenancePassed && Failures.Count == 0;

    public static EvaluationScorecard Combine(string scenarioId, IReadOnlyList<string> trajectoryFailures, IReadOnlyList<string> provenanceFailures)
    {
        var failures = trajectoryFailures.Concat(provenanceFailures).ToArray();
        return new EvaluationScorecard(scenarioId, trajectoryFailures.Count == 0, provenanceFailures.Count == 0, failures);
    }
}
