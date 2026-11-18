using System.Diagnostics;

namespace PublishingTracker.Agent.Telemetry;

public static class AgentTelemetry
{
    public const string ActivitySourceName = "PublishingTracker.Agent";
    public static readonly ActivitySource Source = new(ActivitySourceName, "1.0.0");
}
