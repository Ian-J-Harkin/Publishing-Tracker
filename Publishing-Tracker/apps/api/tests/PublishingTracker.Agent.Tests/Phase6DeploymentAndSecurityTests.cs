using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PublishingTracker.Agent.Auth;
using PublishingTracker.Agent.Configuration;
using Xunit;

namespace PublishingTracker.Agent.Tests;

public sealed class Phase6DeploymentAndSecurityTests
{
    [Fact]
    public void AIProviderOptions_AllowInsecureInternalHttp_FailsFastOutsideDevelopmentOrTesting()
    {
        var options = new AIProviderOptions
        {
            Type = "openrouter",
            ApiKey = "test-key",
            McpEndpoint = "http://api:8080/mcp",
            AllowInsecureInternalHttp = true
        };

        var errorInProd = options.Validate(isDevelopmentOrTesting: false);
        Assert.NotNull(errorInProd);
        Assert.Contains("AIProvider:AllowInsecureInternalHttp cannot be enabled outside Development or Testing", errorInProd);
    }

    [Fact]
    public void AIProviderOptions_AllowInsecureInternalHttp_PermittedInDevelopmentAndTesting()
    {
        var options = new AIProviderOptions
        {
            Type = "openrouter",
            ApiKey = "test-key",
            McpEndpoint = "http://api:8080/mcp",
            AllowInsecureInternalHttp = true
        };

        var errorInDev = options.Validate(isDevelopmentOrTesting: true);
        Assert.Null(errorInDev);
    }

    [Theory]
    [InlineData("https://production.example.com/mcp", false, false, true)]
    [InlineData("https://production.example.com/mcp", true, false, true)]
    [InlineData("http://localhost:7285/mcp", true, false, true)]
    [InlineData("http://127.0.0.1:7285/mcp", true, false, true)]
    [InlineData("http://localhost:7285/mcp", false, false, false)]
    [InlineData("http://api:8080/mcp", true, true, true)]
    [InlineData("http://publishingtracker-api:8080/mcp", true, true, true)]
    [InlineData("http://api:8080/mcp", true, false, false)]
    [InlineData("http://api:8080/mcp", false, true, false)]
    [InlineData("http://evil.attacker.com/mcp", true, true, false)]
    [InlineData("https://production.example.com/mcp?query=leak", true, true, false)]
    [InlineData("https://user:pass@production.example.com/mcp", true, true, false)]
    public void AIProviderOptions_IsEndpointTrusted_EnforcesStrictAllowlist(
        string url,
        bool isDevelopmentOrTesting,
        bool allowInsecureInternalHttp,
        bool expectedTrusted)
    {
        var uri = Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed : null;
        var actual = AIProviderOptions.IsEndpointTrusted(uri, isDevelopmentOrTesting, allowInsecureInternalHttp);
        Assert.Equal(expectedTrusted, actual);
    }

    [Fact]
    public async Task PerRequestBearerHandler_AllowInsecureInternalHttp_PermitsInternalContainerHostInDevelopment()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Bearer"));
        context.Request.Headers.Authorization = "Bearer valid-token";
        var accessor = new HttpContextAccessor { HttpContext = context };

        var capturing = new CapturingHandler();
        var options = Options.Create(new AIProviderOptions
        {
            McpEndpoint = "http://api:8080/mcp",
            AllowInsecureInternalHttp = true
        });

        using var handler = new PerRequestBearerHandler(accessor, options, new TestHostEnvironment("Development"))
        {
            InnerHandler = capturing
        };

        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://api:8080/mcp");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer valid-token", capturing.Authorizations.Single());
    }

    [Fact]
    public async Task PerRequestBearerHandler_AllowInsecureInternalHttp_RejectsWhenDisabledOrInProduction()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Bearer"));
        context.Request.Headers.Authorization = "Bearer valid-token";
        var accessor = new HttpContextAccessor { HttpContext = context };

        // Disabled in Development
        var disabledOptions = Options.Create(new AIProviderOptions
        {
            McpEndpoint = "http://api:8080/mcp",
            AllowInsecureInternalHttp = false
        });
        using var devDisabledHandler = new PerRequestBearerHandler(accessor, disabledOptions, new TestHostEnvironment("Development"))
        {
            InnerHandler = new CapturingHandler()
        };
        using var devDisabledClient = new HttpClient(devDisabledHandler);
        using var devReq = new HttpRequestMessage(HttpMethod.Post, "http://api:8080/mcp");
        await Assert.ThrowsAsync<InvalidOperationException>(() => devDisabledClient.SendAsync(devReq));

        // Enabled but running in Production
        var prodOptions = Options.Create(new AIProviderOptions
        {
            McpEndpoint = "http://api:8080/mcp",
            AllowInsecureInternalHttp = true
        });
        using var prodHandler = new PerRequestBearerHandler(accessor, prodOptions, new TestHostEnvironment("Production"))
        {
            InnerHandler = new CapturingHandler()
        };
        using var prodClient = new HttpClient(prodHandler);
        using var prodReq = new HttpRequestMessage(HttpMethod.Post, "http://api:8080/mcp");
        await Assert.ThrowsAsync<InvalidOperationException>(() => prodClient.SendAsync(prodReq));
    }

    [Fact]
    public async Task HealthProbe_Main_ReturnsZeroOnSuccess_AndOneOnFailure()
    {
        // 1. Invalid URL -> exits with 1
        var invalidExit = await HealthProbe.Program.Main(["not-a-valid-url"]);
        Assert.Equal(1, invalidExit);

        // 2. Unreachable localhost port -> exits with 1
        var unreachableExit = await HealthProbe.Program.Main(["http://127.0.0.1:59999/health/ready", "1"]);
        Assert.Equal(1, unreachableExit);
    }

    [Fact]
    public void JwtKeyValidation_EnforcesMinimum32ByteLength()
    {
        Assert.NotNull(AIProviderOptions.ValidateJwtKey(null));
        Assert.NotNull(AIProviderOptions.ValidateJwtKey("   "));
        Assert.NotNull(AIProviderOptions.ValidateJwtKey("too-short-key-under-32-bytes"));
        Assert.Null(AIProviderOptions.ValidateJwtKey("exactly-32-bytes-minimum-key-123"));
        Assert.Null(AIProviderOptions.ValidateJwtKey("super-secret-jwt-signing-key-with-sufficient-entropy-for-hmac-sha256"));
    }

    [Fact]
    public async Task HealthProbe_Main_ReturnsZeroAgainstHealthyServer_AndOneAgainstUnhealthyServer()
    {
        await using var healthyServer = StartSingleResponseServer("HTTP/1.1 200 OK", "Healthy");
        var healthyExit = await HealthProbe.Program.Main([healthyServer.Url, "3"]);
        await healthyServer.Completed;
        Assert.Equal(0, healthyExit);

        await using var unhealthyServer = StartSingleResponseServer("HTTP/1.1 503 Service Unavailable", "Unhealthy");
        var unhealthyExit = await HealthProbe.Program.Main([unhealthyServer.Url, "3"]);
        await unhealthyServer.Completed;
        Assert.Equal(1, unhealthyExit);
    }

    private static SingleResponseServer StartSingleResponseServer(string statusLine, string body)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var task = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var buffer = new byte[1024];
            var received = new StringBuilder();
            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer);
                if (bytesRead <= 0) break;
                received.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
                if (received.ToString().Contains("\r\n\r\n", StringComparison.Ordinal)) break;
            }

            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var response = $"{statusLine}\r\nContent-Type: text/plain\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n{body}";
            var responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes);
            await stream.FlushAsync();
        });

        return new SingleResponseServer(listener, $"http://127.0.0.1:{port}/health/ready", task);
    }

    private sealed class SingleResponseServer(TcpListener listener, string url, Task completed) : IAsyncDisposable
    {
        public string Url { get; } = url;
        public Task Completed { get; } = completed;

        public async ValueTask DisposeAsync()
        {
            listener.Stop();
            await Completed.ConfigureAwait(false);
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string?> Authorizations { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "PublishingTracker.Agent.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
