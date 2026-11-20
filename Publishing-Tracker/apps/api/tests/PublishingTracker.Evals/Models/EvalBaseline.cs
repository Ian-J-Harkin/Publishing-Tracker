using System.Text.Json;

namespace PublishingTracker.Evals.Models;

/// <summary>
/// Baseline scorecard defining turn budgets, tool call quotas, privacy requirements,
/// and recall targets for regression detection (Step 5.4 deliverable).
/// </summary>
public sealed record EvalBaseline(
    string Version,
    DateTimeOffset GeneratedAt,
    decimal MaxSpendCapUsd,
    EvalPrivacyPolicy PrivacyPolicy,
    IReadOnlyDictionary<string, ScenarioBaseline> Scenarios)
{
    public static EvalBaseline LoadDefault()
    {
        var appPath = AppContext.BaseDirectory;
        var directPath = Path.Combine(appPath, "eval_baseline.json");
        var sourcePath = Path.Combine(appPath, "..", "..", "..", "eval_baseline.json");

        var path = File.Exists(directPath) ? directPath : (File.Exists(sourcePath) ? sourcePath : "eval_baseline.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"eval_baseline.json could not be found at '{path}' or '{directPath}'.");
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<EvalBaseline>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize eval_baseline.json");
    }
}

public sealed record EvalPrivacyPolicy(
    bool RequireDataCollectionDeny,
    bool RequireHttpReferer,
    bool RequireXTitle);

public sealed record ScenarioBaseline(
    int MaxModelTurns,
    int MaxTotalToolCalls,
    double MinExpectedToolsCalledRatio,
    IReadOnlyList<string> ExpectedTools,
    IReadOnlyList<string>? ForbiddenTools,
    IReadOnlyList<string>? ExpectedDocumentIds,
    double? MinRecallAtK,
    int? TopK,
    bool? RejectionExpected);
