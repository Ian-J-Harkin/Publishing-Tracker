namespace PublishingTracker.Agent.Configuration;

public sealed class AgentLoopOptions
{
    public const string SectionName = "AgentLoop";
    public int MaxModelTurns { get; init; } = 6;
    public int MaxTotalToolCalls { get; init; } = 8;
    public int MaxValidationRetries { get; init; } = 2;
    public int RepeatCallLimit { get; init; } = 2;
    public int MaxToolPayloadBytes { get; init; } = 16_384;
    public TimeSpan PerCallTimeout { get; init; } = TimeSpan.FromSeconds(20);
    public TimeSpan OverallTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public int MaxPaginationPages { get; init; } = 2;

    public void Validate()
    {
        if (MaxModelTurns <= 0) throw new InvalidOperationException("MaxModelTurns must be positive.");
        if (MaxTotalToolCalls <= 0) throw new InvalidOperationException("MaxTotalToolCalls must be positive.");
        if (MaxValidationRetries < 0) throw new InvalidOperationException("MaxValidationRetries cannot be negative.");
        if (RepeatCallLimit <= 0) throw new InvalidOperationException("RepeatCallLimit must be positive.");
        if (MaxToolPayloadBytes <= 0) throw new InvalidOperationException("MaxToolPayloadBytes must be positive.");
        if (MaxPaginationPages <= 0) throw new InvalidOperationException("MaxPaginationPages must be positive.");
        if (PerCallTimeout <= TimeSpan.Zero) throw new InvalidOperationException("PerCallTimeout must be greater than zero.");
        if (OverallTimeout <= PerCallTimeout) throw new InvalidOperationException("OverallTimeout must be greater than PerCallTimeout.");
    }
}
