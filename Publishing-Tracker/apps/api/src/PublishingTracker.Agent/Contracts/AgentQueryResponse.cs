namespace PublishingTracker.Agent.Contracts;

public sealed record AgentQueryResponse(
    string Status,
    string? Answer,
    IReadOnlyList<string> ToolsUsed,
    int Turns,
    int TotalToolCalls,
    IReadOnlyList<string> Limitations,
    string? Error);
