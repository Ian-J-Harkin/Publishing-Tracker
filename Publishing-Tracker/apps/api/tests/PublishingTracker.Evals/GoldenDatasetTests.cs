using PublishingTracker.Evals.Models;

namespace PublishingTracker.Evals;

public sealed class GoldenDatasetTests
{
    [Fact]
    public void Dataset_HasTwelveCanonicalBenchmarks_WithUniqueIds()
    {
        Assert.Equal(12, GoldenDataset.Scenarios.Count);
        var ids = GoldenDataset.Scenarios.Select(s => s.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(AllScenarios))]
    public void Scenario_HasNonEmptyPromptAndNoOverlapBetweenExpectedAndForbiddenTools(EvalScenario scenario)
    {
        Assert.False(string.IsNullOrWhiteSpace(scenario.Prompt));
        Assert.False(string.IsNullOrWhiteSpace(scenario.Description));
        Assert.Empty(scenario.ExpectedTools.Intersect(scenario.ForbiddenTools, StringComparer.Ordinal));
    }

    public static IEnumerable<object[]> AllScenarios() =>
        GoldenDataset.Scenarios.Select(s => new object[] { s });
}
