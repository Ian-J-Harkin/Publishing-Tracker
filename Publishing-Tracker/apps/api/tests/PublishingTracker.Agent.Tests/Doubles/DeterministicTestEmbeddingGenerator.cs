using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using PublishingTracker.Rag.Services;

namespace PublishingTracker.Agent.Tests.Doubles;

/// <summary>
/// Offline deterministic 1536-dim embedding generator mapping domain keywords to orthogonal axes
/// so similarity ranking tests run in milliseconds without external API spend.
/// </summary>
public sealed class DeterministicTestEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly Func<string, float[]>? _overrideFactory;

    public int CallCount { get; private set; }
    public List<string> Inputs { get; } = [];

    public DeterministicTestEmbeddingGenerator(Func<string, float[]>? overrideFactory = null) =>
        _overrideFactory = overrideFactory;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var list = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var value in values)
        {
            CallCount++;
            Inputs.Add(value);
            var vec = _overrideFactory?.Invoke(value) ?? EncodeText(value);
            list.Add(new Embedding<float>(vec));
        }
        return Task.FromResult(list);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }

    public static float[] EncodeText(string text)
    {
        var vector = new float[VectorMath.ExpectedDimensions];
        var lower = (text ?? string.Empty).ToLowerInvariant();

        if (lower.Contains("audiobook") || lower.Contains("narrator") || lower.Contains("duration"))
            vector[0] += 5.0f;
        if (lower.Contains("ebook") || lower.Contains("filesize") || lower.Contains("asin"))
            vector[1] += 5.0f;
        if (lower.Contains("paperback") || lower.Contains("hardcover") || lower.Contains("isbn") || lower.Contains("dimensions"))
            vector[2] += 5.0f;
        if (lower.Contains("series") || lower.Contains("description") || lower.Contains("genre"))
            vector[3] += 5.0f;
        if (lower.Contains("platform") || lower.Contains("published") || lower.Contains("kdp"))
            vector[4] += 5.0f;

        // Stable small fallback hash component across dimensions 16..31 so any non-empty text has norm > 0
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(lower));
        for (var i = 0; i < 16; i++)
        {
            vector[16 + i] = (hash[i] + 1) / 256.0f;
        }

        return vector;
    }
}
