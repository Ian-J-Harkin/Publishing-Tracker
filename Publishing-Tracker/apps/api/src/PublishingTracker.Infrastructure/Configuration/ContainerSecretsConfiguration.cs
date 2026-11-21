using Microsoft.Extensions.Configuration;

namespace PublishingTracker.Infrastructure.Configuration;

public static class ContainerSecretsConfiguration
{
    public const string DefaultSecretsDirectory = "/run/secrets";

    public static IConfigurationBuilder AddContainerSecrets(
        this IConfigurationBuilder builder,
        string directoryPath = DefaultSecretsDirectory)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var loaded = LoadSecretsFromDirectory(directoryPath);
        if (loaded.Count > 0)
        {
            builder.AddInMemoryCollection(loaded);
        }

        return builder;
    }

    public static IReadOnlyDictionary<string, string?> LoadSecretsFromDirectory(
        string directoryPath = DefaultSecretsDirectory)
    {
        var secrets = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return secrets;
        }

        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(filePath);
            if (string.IsNullOrWhiteSpace(fileName) || fileName.StartsWith('.'))
            {
                continue;
            }

            var value = File.ReadAllText(filePath).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var configKey = MapFileNameToConfigurationKey(fileName);
            secrets[configKey] = value;
        }

        return secrets;
    }

    private static string MapFileNameToConfigurationKey(string fileName)
    {
        var normalized = fileName;
        if (normalized.EndsWith(".secret", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^".secret".Length];
        }
        else if (normalized.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^".txt".Length];
        }

        return normalized.ToLowerInvariant() switch
        {
            "ai_provider_api_key" or "openai_api_key" => "AIProvider:ApiKey",
            "gemini_api_key" or "google_api_key" => "AIProvider:GeminiApiKey",
            "openrouter_api_key" => "AIProvider:OpenRouterApiKey",
            "jwt_key" or "jwt_signing_key" => "Jwt:Key",
            "neon_connection" => "ConnectionStrings:NeonConnection",
            "default_connection" => "ConnectionStrings:DefaultConnection",
            _ => normalized.Replace("__", ":", StringComparison.Ordinal)
        };
    }
}
