using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using OpenAI;
using PublishingTracker.Agent.Telemetry;

namespace PublishingTracker.Agent.Configuration;

public static class AIProviderSetup
{
    public const string OpenRouterDefaultEndpoint = "https://openrouter.ai/api/v1";
    public const string OpenRouterDefaultModelId = "anthropic/claude-sonnet-4";
    public const string OpenAiDefaultEndpoint = "https://api.openai.com/v1";
    public const string OpenAiDefaultModelId = "gpt-4o-mini";
    public const string OllamaDefaultEndpoint = "http://localhost:11434/v1";
    public const string OllamaDefaultModelId = "qwen2.5-coder:7b";

    public const string OpenRouterDefaultEmbeddingModelId = "openai/text-embedding-3-small";
    public const string OpenAiDefaultEmbeddingModelId = "text-embedding-3-small";
    public const string OllamaDefaultEmbeddingModelId = "nomic-embed-text";

    public static AIProviderOptions Resolve(AIProviderOptions configured, Func<string, string?> getEnv)
    {
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(getEnv);

        var type = configured.Type?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AIProviderOptions.SupportedTypes.Contains(type, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported AIProvider:Type '{configured.Type}'. Supported values: {string.Join(", ", AIProviderOptions.SupportedTypes)}.");
        }

        var endpoint = type switch
        {
            "openrouter" => First(getEnv("OPENROUTER_ENDPOINT"), configured.OpenRouterEndpoint, configured.Endpoint, OpenRouterDefaultEndpoint),
            "openai" => First(getEnv("OPENAI_ENDPOINT"), ConfiguredOpenAiEndpoint(configured), OpenAiDefaultEndpoint),
            _ => First(getEnv("OLLAMA_ENDPOINT"), configured.OllamaEndpoint, configured.Endpoint, OllamaDefaultEndpoint)
        };
        var model = type switch
        {
            "openrouter" => First(getEnv("OPENROUTER_MODEL_NAME"), configured.OpenRouterModelId, configured.ModelId, OpenRouterDefaultModelId),
            "openai" => First(getEnv("OPENAI_MODEL_NAME"), configured.ModelId, OpenAiDefaultModelId),
            _ => First(getEnv("OLLAMA_MODEL_NAME"), configured.OllamaModelId, configured.ModelId, OllamaDefaultModelId)
        };
        var key = type switch
        {
            "openrouter" => First(getEnv("OPENROUTER_API_KEY"), configured.OpenRouterApiKey, configured.ApiKey),
            "openai" => First(getEnv("OPENAI_API_KEY"), configured.ApiKey),
            _ => First(configured.ApiKey, "ollama")
        };
        var embeddingModel = type switch
        {
            "openrouter" => First(getEnv("OPENROUTER_EMBEDDING_MODEL_NAME"), getEnv("EMBEDDING_MODEL_NAME"), configured.EmbeddingModelId, OpenRouterDefaultEmbeddingModelId),
            "openai" => First(getEnv("OPENAI_EMBEDDING_MODEL_NAME"), getEnv("EMBEDDING_MODEL_NAME"), ConfiguredOpenAiEmbeddingModel(configured), OpenAiDefaultEmbeddingModelId),
            _ => First(getEnv("OLLAMA_EMBEDDING_MODEL_NAME"), getEnv("EMBEDDING_MODEL_NAME"), configured.OllamaEmbeddingModelId, OllamaDefaultEmbeddingModelId)
        };

        return new AIProviderOptions
        {
            Type = type,
            ModelId = model!,
            Endpoint = endpoint,
            ApiKey = key ?? string.Empty,
            OpenRouterApiKey = string.Empty,
            OpenRouterModelId = type == "openrouter" ? model! : configured.OpenRouterModelId,
            OpenRouterEndpoint = type == "openrouter" ? endpoint : configured.OpenRouterEndpoint,
            OllamaModelId = type == "ollama" ? model! : configured.OllamaModelId,
            OllamaEndpoint = type == "ollama" ? endpoint : configured.OllamaEndpoint,
            McpEndpoint = configured.McpEndpoint,
            AllowInsecureInternalHttp = configured.AllowInsecureInternalHttp,
            EmbeddingModelId = embeddingModel!,
            OllamaEmbeddingModelId = type == "ollama" ? embeddingModel! : configured.OllamaEmbeddingModelId,
            EmbeddingDimensions = configured.EmbeddingDimensions
        };
    }

    public static IChatClient CreateChatClient(AIProviderOptions options, HttpClient? customHttpClient = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var validationError = options.Validate();
        if (validationError is not null) throw new InvalidOperationException(validationError);

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(options.Endpoint!)
        };

        if (customHttpClient is not null)
        {
            clientOptions.Transport = new HttpClientPipelineTransport(customHttpClient);
        }

        if (string.Equals(options.Type?.Trim(), "openrouter", StringComparison.OrdinalIgnoreCase))
        {
            clientOptions.AddPolicy(new OpenRouterHeadersPolicy(), PipelinePosition.PerCall);
        }

        var client = new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);
        return client
            .GetChatClient(options.ModelId)
            .AsIChatClient()
            .AsBuilder()
            .UseOpenTelemetry(
                sourceName: AgentTelemetry.ActivitySourceName,
                configure: telemetry => telemetry.EnableSensitiveData = false)
            .Build();
    }

    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(
        AIProviderOptions options,
        HttpClient? customHttpClient = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var validationError = options.Validate();
        if (validationError is not null) throw new InvalidOperationException(validationError);

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(options.Endpoint!)
        };

        if (customHttpClient is not null)
        {
            clientOptions.Transport = new HttpClientPipelineTransport(customHttpClient);
        }

        if (string.Equals(options.Type?.Trim(), "openrouter", StringComparison.OrdinalIgnoreCase))
        {
            clientOptions.AddPolicy(new OpenRouterHeadersPolicy(), PipelinePosition.PerCall);
        }

        var client = new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);
        return client
            .GetEmbeddingClient(options.EmbeddingModelId)
            .AsIEmbeddingGenerator()
            .AsBuilder()
            .UseOpenTelemetry(
                sourceName: AgentTelemetry.ActivitySourceName,
                configure: telemetry => telemetry.EnableSensitiveData = false)
            .Build();
    }

    private static string? ConfiguredOpenAiEndpoint(AIProviderOptions options) =>
        string.Equals(options.Endpoint, OpenRouterDefaultEndpoint, StringComparison.OrdinalIgnoreCase)
            ? null
            : options.Endpoint;

    private static string? ConfiguredOpenAiEmbeddingModel(AIProviderOptions options) =>
        string.Equals(options.EmbeddingModelId, OpenRouterDefaultEmbeddingModelId, StringComparison.OrdinalIgnoreCase)
            ? null
            : options.EmbeddingModelId;

    private static string? First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private sealed class OpenRouterHeadersPolicy : PipelinePolicy
    {
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            ApplyHeaders(message);
            ProcessNext(message, pipeline, currentIndex);
        }

        public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            ApplyHeaders(message);
            return ProcessNextAsync(message, pipeline, currentIndex);
        }

        private static void ApplyHeaders(PipelineMessage message)
        {
            message.Request.Headers.Set("HTTP-Referer", "https://publishingtracker.local");
            message.Request.Headers.Set("X-Title", "PublishingTracker");
            message.Request.Headers.Set("data_collection", "deny");
        }
    }
}
