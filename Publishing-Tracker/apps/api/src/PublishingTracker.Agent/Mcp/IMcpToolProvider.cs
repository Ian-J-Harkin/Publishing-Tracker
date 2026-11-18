using Microsoft.Extensions.AI;

namespace PublishingTracker.Agent.Mcp;

public interface IMcpToolSession : IAsyncDisposable
{
    IReadOnlyList<AIFunction> Tools { get; }
}

public interface IMcpToolProvider
{
    Task<IMcpToolSession> OpenSessionAsync(CancellationToken cancellationToken = default);
}
