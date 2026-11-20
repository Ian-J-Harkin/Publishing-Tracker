using Microsoft.Extensions.AI;
using PublishingTracker.Rag.Contracts;
using PublishingTracker.Rag.Persistence;

namespace PublishingTracker.Rag.Services;

public sealed class RagRetrievalService(
    IRagChunkStore store,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
    public const int MaxQueryLength = 500;
    public const int MaxTopK = 8;

    public async Task<RagSearchResultDto> SearchAsync(
        int authenticatedUserId,
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (authenticatedUserId <= 0)
            throw new UnauthorizedAccessException("Authenticated positive userId is required.");
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length > MaxQueryLength)
            throw new ArgumentException($"INVALID_ARGUMENT: query must be between 1 and {MaxQueryLength} characters.", nameof(query));
        if (topK < 1 || topK > MaxTopK)
            throw new ArgumentOutOfRangeException(nameof(topK), $"INVALID_ARGUMENT: topK must be between 1 and {MaxTopK}.");

        var trimmedQuery = query.Trim();
        var queryVector = await embeddingGenerator.GenerateVectorAsync(
            trimmedQuery,
            new EmbeddingGenerationOptions { Dimensions = VectorMath.ExpectedDimensions },
            cancellationToken);
        VectorMath.ValidateEmbedding(queryVector.Span);

        var hits = await store.SearchByVectorAsync(authenticatedUserId, queryVector, topK, cancellationToken);
        return new RagSearchResultDto(trimmedQuery, topK, hits);
    }
}
