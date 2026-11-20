using Microsoft.Extensions.AI;
using PublishingTracker.Rag.Tools;

namespace PublishingTracker.Agent.Mcp;

public sealed class CompositeToolProvider(
    IMcpToolProvider mcpProvider,
    IRagSearchToolProvider ragToolProvider) : IMcpToolProvider
{
    public async Task<IMcpToolSession> OpenSessionAsync(CancellationToken cancellationToken = default)
    {
        // Validate positive integer caller identity BEFORE opening any remote /mcp session.
        var callerUserId = ragToolProvider.ResolveAuthenticatedUserId();
        var mcpSession = await mcpProvider.OpenSessionAsync(cancellationToken);

        try
        {
            var ragTools = ragToolProvider.CreateToolsForCaller(callerUserId);
            var combined = mcpSession.Tools.Concat(ragTools).ToArray();
            return new CompositeToolSession(mcpSession, combined);
        }
        catch
        {
            await mcpSession.DisposeAsync();
            throw;
        }
    }

    private sealed class CompositeToolSession(IMcpToolSession inner, IReadOnlyList<AIFunction> tools) : IMcpToolSession
    {
        public IReadOnlyList<AIFunction> Tools { get; } = tools;
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
