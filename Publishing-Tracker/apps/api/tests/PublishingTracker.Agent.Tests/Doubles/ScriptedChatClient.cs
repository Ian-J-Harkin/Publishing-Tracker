using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace PublishingTracker.Agent.Tests.Doubles;

public sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<ChatResponse>>> _responses = new();
    public List<IReadOnlyList<ChatMessage>> Invocations { get; } = [];

    public void EnqueueFunctionCalls(params (string Name, IDictionary<string, object?> Arguments)[] calls)
    {
        _responses.Enqueue((_, _) => Task.FromResult(new ChatResponse(new ChatMessage(
            ChatRole.Assistant,
            calls.Select(call => (AIContent)new FunctionCallContent(Guid.NewGuid().ToString("N"), call.Name, call.Arguments)).ToList()))));
    }

    public void EnqueueText(string text) =>
        _responses.Enqueue((_, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text))));

    public void Enqueue(Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<ChatResponse>> response) => _responses.Enqueue(response);

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var snapshot = messages.ToArray();
        Invocations.Add(snapshot);
        if (!_responses.TryDequeue(out var response)) throw new InvalidOperationException("No scripted response remaining.");
        return response(snapshot, cancellationToken);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotSupportedException("Streaming is not used by Phase 2.");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
