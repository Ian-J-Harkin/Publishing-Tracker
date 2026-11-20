namespace PublishingTracker.Evals.Models;

/// <summary>
/// A single canonical evaluation benchmark: a prompt plus the trajectory/provenance
/// expectations that a passing agent run must satisfy. Backs the Phase 5 golden dataset
/// (<see cref="GoldenDataset"/>) consumed by Layer 1 (offline) and Layer 2 (integration) evals.
/// </summary>
public sealed record EvalScenario(
    string Id,
    string Description,
    string Prompt,
    IReadOnlyList<string> ExpectedTools,
    IReadOnlyList<string> ForbiddenTools,
    IReadOnlyList<string> ExpectedDocumentIds,
    bool ExpectAnswered);
