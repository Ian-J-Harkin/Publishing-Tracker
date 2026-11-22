using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public const string GeminiDefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai";
    public const string GeminiDefaultModelId = "gemini-2.5-flash";
    public const string OllamaDefaultEndpoint = "http://localhost:11434/v1";
    public const string OllamaDefaultModelId = "qwen2.5-coder:7b";

    public const string OpenRouterDefaultEmbeddingModelId = "openai/text-embedding-3-small";
    public const string OpenAiDefaultEmbeddingModelId = "text-embedding-3-small";
    public const string GeminiDefaultEmbeddingModelId = "gemini-embedding-001";
    public const string OllamaDefaultEmbeddingModelId = "nomic-embed-text";

    public static AIProviderOptions Resolve(
        AIProviderOptions configured,
        Func<string, string?> getEnv,
        Func<string, string?>? readSecretFile = null)
    {
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(getEnv);
        var reader = readSecretFile ?? TryReadSecretFile;

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
            "gemini" => First(getEnv("GEMINI_ENDPOINT"), configured.GeminiEndpoint, ConfiguredGeminiEndpoint(configured), GeminiDefaultEndpoint),
            _ => First(getEnv("OLLAMA_ENDPOINT"), configured.OllamaEndpoint, configured.Endpoint, OllamaDefaultEndpoint)
        };
        var model = type switch
        {
            "openrouter" => First(getEnv("OPENROUTER_MODEL_NAME"), configured.OpenRouterModelId, configured.ModelId, OpenRouterDefaultModelId),
            "openai" => First(getEnv("OPENAI_MODEL_NAME"), configured.ModelId, OpenAiDefaultModelId),
            "gemini" => First(getEnv("GEMINI_MODEL_NAME"), configured.GeminiModelId, ConfiguredGeminiModel(configured), GeminiDefaultModelId),
            _ => First(getEnv("OLLAMA_MODEL_NAME"), configured.OllamaModelId, configured.ModelId, OllamaDefaultModelId)
        };
        var key = type switch
        {
            "openrouter" => First(
                FromFileEnv(getEnv, reader, "OPENROUTER_API_KEY_FILE", "/run/secrets/openrouter_api_key"),
                FromFileEnv(getEnv, reader, "AI_PROVIDER_API_KEY_FILE", "/run/secrets/ai_provider_api_key"),
                getEnv("OPENROUTER_API_KEY"),
                configured.OpenRouterApiKey,
                configured.ApiKey),
            "openai" => First(
                FromFileEnv(getEnv, reader, "OPENAI_API_KEY_FILE", "/run/secrets/openai_api_key"),
                FromFileEnv(getEnv, reader, "AI_PROVIDER_API_KEY_FILE", "/run/secrets/ai_provider_api_key"),
                getEnv("OPENAI_API_KEY"),
                configured.ApiKey),
            "gemini" => First(
                FromFileEnv(getEnv, reader, "GEMINI_API_KEY_FILE", "/run/secrets/gemini_api_key"),
                FromFileEnv(getEnv, reader, "GOOGLE_API_KEY_FILE", "/run/secrets/google_api_key"),
                FromFileEnv(getEnv, reader, "AI_PROVIDER_API_KEY_FILE", "/run/secrets/ai_provider_api_key"),
                getEnv("GEMINI_API_KEY"),
                getEnv("GOOGLE_API_KEY"),
                configured.GeminiApiKey,
                configured.ApiKey),
            _ => First(configured.ApiKey, "ollama")
        };
        var embeddingModel = type switch
        {
            "openrouter" => First(getEnv("OPENROUTER_EMBEDDING_MODEL_NAME"), getEnv("EMBEDDING_MODEL_NAME"), configured.EmbeddingModelId, OpenRouterDefaultEmbeddingModelId),
            "openai" => First(getEnv("OPENAI_EMBEDDING_MODEL_NAME"), getEnv("EMBEDDING_MODEL_NAME"), ConfiguredOpenAiEmbeddingModel(configured), OpenAiDefaultEmbeddingModelId),
            "gemini" => First(getEnv("GEMINI_EMBEDDING_MODEL_NAME"), getEnv("EMBEDDING_MODEL_NAME"), ConfiguredGeminiEmbeddingModel(configured), GeminiDefaultEmbeddingModelId),
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
            GeminiApiKey = string.Empty,
            GeminiModelId = type == "gemini" ? model! : configured.GeminiModelId,
            GeminiEndpoint = type == "gemini" ? endpoint : configured.GeminiEndpoint,
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

    private static string? ConfiguredGeminiEndpoint(AIProviderOptions options) =>
        string.Equals(options.Endpoint, OpenRouterDefaultEndpoint, StringComparison.OrdinalIgnoreCase)
            ? null
            : options.Endpoint;

    private static string? ConfiguredGeminiModel(AIProviderOptions options) =>
        string.Equals(options.ModelId, OpenRouterDefaultModelId, StringComparison.OrdinalIgnoreCase)
            ? null
            : options.ModelId;

    private static string? ConfiguredGeminiEmbeddingModel(AIProviderOptions options) =>
        string.Equals(options.EmbeddingModelId, OpenRouterDefaultEmbeddingModelId, StringComparison.OrdinalIgnoreCase)
            ? null
            : options.EmbeddingModelId;

    private static string? First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? FromFileEnv(
        Func<string, string?> getEnv,
        Func<string, string?> reader,
        string envVarName,
        string? defaultSecretPath = null)
    {
        var configuredPath = getEnv(envVarName);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fromConfigured = reader(configuredPath.Trim());
            if (!string.IsNullOrWhiteSpace(fromConfigured))
            {
                return fromConfigured.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(defaultSecretPath))
        {
            var fromDefault = reader(defaultSecretPath);
            if (!string.IsNullOrWhiteSpace(fromDefault))
            {
                return fromDefault.Trim();
            }
        }

        return null;
    }

    private static string? TryReadSecretFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var content = File.ReadAllText(path).Trim();
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }

    private sealed class OpenRouterHeadersPolicy : PipelinePolicy
    {
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            ApplyHeadersAndPrivacyBody(message);
            ProcessNext(message, pipeline, currentIndex);
        }

        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            await ApplyHeadersAndPrivacyBodyAsync(message).ConfigureAwait(false);
            await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
        }

        private static void ApplyHeadersAndPrivacyBody(PipelineMessage message)
        {
            ApplyHeaders(message);
            if (message.Request.Content is null)
            {
                return;
            }

            using var buffer = new MemoryStream();
            message.Request.Content.WriteTo(buffer, message.CancellationToken);
            if (TryInjectDataCollectionDeny(buffer.ToArray(), out var updatedBytes))
            {
                message.Request.Content = BinaryContent.Create(BinaryData.FromBytes(updatedBytes));
            }
        }

        private static async ValueTask ApplyHeadersAndPrivacyBodyAsync(PipelineMessage message)
        {
            ApplyHeaders(message);
            if (message.Request.Content is null)
            {
                return;
            }

            using var buffer = new MemoryStream();
            await message.Request.Content.WriteToAsync(buffer, message.CancellationToken).ConfigureAwait(false);
            if (TryInjectDataCollectionDeny(buffer.ToArray(), out var updatedBytes))
            {
                message.Request.Content = BinaryContent.Create(BinaryData.FromBytes(updatedBytes));
            }
        }

        private static void ApplyHeaders(PipelineMessage message)
        {
            message.Request.Headers.Set("HTTP-Referer", "https://publishingtracker.local");
            message.Request.Headers.Set("X-Title", "PublishingTracker");
            message.Request.Headers.Set("data_collection", "deny");
        }

        private static bool TryInjectDataCollectionDeny(byte[] rawBytes, out byte[] updatedBytes)
        {
            updatedBytes = rawBytes;
            if (rawBytes.Length == 0)
            {
                return false;
            }

            try
            {
                if (JsonNode.Parse(rawBytes) is not JsonObject jsonObj)
                {
                    return false;
                }

                if (jsonObj["provider"] is not JsonObject providerObj)
                {
                    providerObj = new JsonObject();
                    jsonObj["provider"] = providerObj;
                }

                providerObj["data_collection"] = "deny";
                updatedBytes = JsonSerializer.SerializeToUtf8Bytes(jsonObj);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
