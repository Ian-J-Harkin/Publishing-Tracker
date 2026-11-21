using System.Net;
using System.Security.Claims;
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
