namespace PublishingTracker.Agent.Orchestration;

public sealed record AgentRunResult(
    bool IsSuccess,
    string? Answer,
    IReadOnlyList<string> ToolsUsed,
    int Turns,
    int TotalToolCalls,
    IReadOnlyList<string> Limitations,
    string? ErrorMessage)
{
    public static AgentRunResult Completed(string answer, IReadOnlyList<string> toolsUsed, int turns, int toolCalls, IReadOnlyList<string> limitations) =>
        new(true, answer, toolsUsed, turns, toolCalls, limitations, null);

    public static AgentRunResult Failed(string error, IReadOnlyList<string> toolsUsed, int turns, int toolCalls) =>
        new(false, null, toolsUsed, turns, toolCalls, Array.Empty<string>(), error);
}
