using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using PublishingTracker.Agent.Configuration;

namespace PublishingTracker.Agent.Mcp;

public sealed class StreamableHttpMcpToolProvider(HttpClient httpClient, IOptions<AIProviderOptions> options) : IMcpToolProvider
{
    private readonly Uri _endpoint = new(options.Value.McpEndpoint);

    public async Task<IMcpToolSession> OpenSessionAsync(CancellationToken cancellationToken = default)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = _endpoint,
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            ownsHttpClient: false);

        var client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
        try
        {
            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            return new McpToolSession(client, tools.Cast<AIFunction>().ToArray());
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }

    private sealed class McpToolSession(McpClient client, IReadOnlyList<AIFunction> tools) : IMcpToolSession
    {
        public IReadOnlyList<AIFunction> Tools { get; } = tools;
        public ValueTask DisposeAsync() => client.DisposeAsync();
    }
}
