using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using PublishingTracker.Agent.Configuration;

namespace PublishingTracker.Agent.Auth;

public sealed class PerRequestBearerHandler(
    IHttpContextAccessor httpContextAccessor,
    IOptions<AIProviderOptions> options,
    IHostEnvironment environment) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ValidateDestination(request.RequestUri);

        var context = httpContextAccessor.HttpContext
            ?? throw new UnauthorizedAccessException("No active caller context for MCP request.");
        if (context.User.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Caller must be authenticated.");

        if (context.Request.Headers.Authorization.Count != 1)
            throw new UnauthorizedAccessException("Exactly one Authorization header is required.");

        var rawAuthorization = context.Request.Headers.Authorization[0];
        if (string.IsNullOrWhiteSpace(rawAuthorization) ||
            !rawAuthorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Missing or invalid bearer credential.");

        var token = rawAuthorization[7..].Trim();
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace))
            throw new UnauthorizedAccessException("Bearer token must not be empty or contain whitespace.");

        request.Headers.Remove("Authorization");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return base.SendAsync(request, cancellationToken);
    }

    private void ValidateDestination(Uri? actual)
    {
        if (actual is null || !actual.IsAbsoluteUri || actual.UserInfo.Length != 0 ||
            actual.Query.Length != 0 || actual.Fragment.Length != 0)
            throw new InvalidOperationException("Target MCP URI must be an absolute endpoint without query or fragment.");

        if (!Uri.TryCreate(options.Value.McpEndpoint, UriKind.Absolute, out var expected))
            throw new InvalidOperationException("Configured MCP endpoint is invalid.");

        var sameDestination = string.Equals(actual.Scheme, expected.Scheme, StringComparison.OrdinalIgnoreCase) &&
                              string.Equals(actual.Host, expected.Host, StringComparison.OrdinalIgnoreCase) &&
                              actual.Port == expected.Port &&
                              string.Equals(NormalizePath(actual.AbsolutePath), NormalizePath(expected.AbsolutePath), StringComparison.Ordinal);
        if (!sameDestination || !string.Equals(NormalizePath(actual.AbsolutePath), "/mcp", StringComparison.Ordinal))
            throw new InvalidOperationException("Target URI does not match the configured trusted MCP endpoint.");

        var isDevOrTest = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        if (!AIProviderOptions.IsEndpointTrusted(actual, isDevOrTest, options.Value.AllowInsecureInternalHttp))
            throw new InvalidOperationException("Insecure or untrusted MCP endpoints are not permitted in the current environment.");
    }

    private static string NormalizePath(string path) => path.TrimEnd('/').ToLowerInvariant();
}
