using Microsoft.Extensions.AI;
using PublishingTracker.Agent.Mcp;

namespace PublishingTracker.Agent.Tests.Doubles;

public sealed class StubToolProvider(params AIFunction[] tools) : IMcpToolProvider
{
    public int OpenCount { get; private set; }
    public int DisposeCount { get; private set; }

    public Task<IMcpToolSession> OpenSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OpenCount++;
        return Task.FromResult<IMcpToolSession>(new Session(tools, () => DisposeCount++));
    }

    public static AIFunction CreateTool(string name, Func<int, string?, int, CancellationToken, Task<object?>> handler)
    {
        StubToolDelegate invoker =
            (int page = 1, string? format = null, int bookId = 0, CancellationToken cancellationToken = default) =>
                handler(page, format, bookId, cancellationToken);
        return AIFunctionFactory.Create(invoker, new AIFunctionFactoryOptions
        {
            Name = name,
            Description = name,
            MarshalResult = (result, _, _) => ValueTask.FromResult(result)
        });
    }

    private delegate Task<object?> StubToolDelegate(int page = 1, string? format = null, int bookId = 0, CancellationToken cancellationToken = default);

    private sealed class Session(IReadOnlyList<AIFunction> tools, Action onDispose) : IMcpToolSession
    {
        public IReadOnlyList<AIFunction> Tools { get; } = tools;
        public ValueTask DisposeAsync()
        {
            onDispose();
            return ValueTask.CompletedTask;
        }
    }
}
