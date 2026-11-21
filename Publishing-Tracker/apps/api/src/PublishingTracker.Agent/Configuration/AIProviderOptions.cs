using System.Text.Json.Serialization;

namespace PublishingTracker.Agent.Configuration;

public sealed class AIProviderOptions
{
    public const string SectionName = "AIProvider";
    public static readonly string[] SupportedTypes = ["openrouter", "openai", "ollama"];

    public string Type { get; set; } = "openrouter";
    public string ModelId { get; set; } = AIProviderSetup.OpenRouterDefaultModelId;
    public string? Endpoint { get; set; } = AIProviderSetup.OpenRouterDefaultEndpoint;
    [JsonIgnore]
    public string ApiKey { get; set; } = string.Empty;
    public string OpenRouterModelId { get; set; } = AIProviderSetup.OpenRouterDefaultModelId;
    public string? OpenRouterEndpoint { get; set; } = AIProviderSetup.OpenRouterDefaultEndpoint;
    [JsonIgnore]
    public string OpenRouterApiKey { get; set; } = string.Empty;
    public string OllamaModelId { get; set; } = AIProviderSetup.OllamaDefaultModelId;
    public string? OllamaEndpoint { get; set; } = AIProviderSetup.OllamaDefaultEndpoint;
    public string McpEndpoint { get; set; } = "https://localhost:7285/mcp";
    public bool AllowInsecureInternalHttp { get; set; }

    public string EmbeddingModelId { get; set; } = AIProviderSetup.OpenRouterDefaultEmbeddingModelId;
    public string OllamaEmbeddingModelId { get; set; } = AIProviderSetup.OllamaDefaultEmbeddingModelId;
    public int EmbeddingDimensions { get; set; } = 1536;

    public string? Validate(bool isDevelopmentOrTesting = true)
    {
        if (AllowInsecureInternalHttp && !isDevelopmentOrTesting)
            return "AIProvider:AllowInsecureInternalHttp cannot be enabled outside Development or Testing environments.";

        var normalizedType = Type?.Trim() ?? string.Empty;
        if (!SupportedTypes.Contains(normalizedType, StringComparer.OrdinalIgnoreCase))
            return $"Unsupported AIProvider:Type '{Type}'. Supported values: {string.Join(", ", SupportedTypes)}.";

        if (string.IsNullOrWhiteSpace(ModelId) || !Uri.TryCreate(Endpoint, UriKind.Absolute, out var providerUri) ||
            providerUri.Scheme is not ("http" or "https") || providerUri.UserInfo.Length != 0 ||
            providerUri.Query.Length != 0 || providerUri.Fragment.Length != 0)
            return "AI provider model and absolute HTTP(S) endpoint without embedded credentials or query values are required.";

        if (string.IsNullOrWhiteSpace(EmbeddingModelId))
            return "AIProvider:EmbeddingModelId is required.";

        if (EmbeddingDimensions != 1536)
            return "AIProvider:EmbeddingDimensions must be 1536 to match the locked vector(1536) schema.";

        var providerUsesInsecureHttp = providerUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        var isOllamaLoopback = providerUri.IsLoopback || providerUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        if (providerUsesInsecureHttp && (!string.Equals(normalizedType, "ollama", StringComparison.OrdinalIgnoreCase) || !isOllamaLoopback))
            return "Only loopback Ollama endpoints may use HTTP; hosted providers must use HTTPS.";

        if (!string.Equals(normalizedType, "ollama", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(ApiKey))
            return $"API key is required for provider '{Type}'. Configure its key using a secret source.";

        if (!Uri.TryCreate(McpEndpoint, UriKind.Absolute, out var mcpUri) ||
            !string.Equals(mcpUri.AbsolutePath.TrimEnd('/'), "/mcp", StringComparison.OrdinalIgnoreCase) ||
            mcpUri.Query.Length != 0 || mcpUri.Fragment.Length != 0 || mcpUri.UserInfo.Length != 0)
            return "AIProvider:McpEndpoint must be an absolute /mcp URL without query, fragment, or user information.";

        if (!IsEndpointTrusted(mcpUri, isDevelopmentOrTesting, AllowInsecureInternalHttp))
            return "AIProvider:McpEndpoint is not trusted in the current environment.";

        return null;
    }

    public static bool IsEndpointTrusted(Uri? actual, bool isDevelopmentOrTesting, bool allowInsecureInternalHttp)
    {
        if (actual is null || !actual.IsAbsoluteUri || actual.UserInfo.Length != 0 ||
            actual.Query.Length != 0 || actual.Fragment.Length != 0)
            return false;

        if (actual.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!actual.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!isDevelopmentOrTesting)
            return false;

        if (actual.IsLoopback || actual.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (allowInsecureInternalHttp &&
            (actual.Host.Equals("api", StringComparison.OrdinalIgnoreCase) ||
             actual.Host.Equals("publishingtracker-api", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    public override string ToString() =>
        $"AIProviderOptions {{ Type = {Type}, ModelId = {ModelId}, EmbeddingModelId = {EmbeddingModelId}, Endpoint = {Endpoint}, ApiKey = {Redact(ApiKey)}, OpenRouterApiKey = {Redact(OpenRouterApiKey)}, McpEndpoint = {McpEndpoint}, AllowInsecureInternalHttp = {AllowInsecureInternalHttp} }}";

    private static string Redact(string? value) => string.IsNullOrWhiteSpace(value) ? "<empty>" : "[REDACTED]";
}
