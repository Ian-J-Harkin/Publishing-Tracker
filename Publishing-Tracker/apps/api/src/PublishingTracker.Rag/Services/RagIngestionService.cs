using Microsoft.Extensions.AI;
using PublishingTracker.Rag.Entities;
using PublishingTracker.Rag.Persistence;

namespace PublishingTracker.Rag.Services;

public sealed class RagIngestionService(
    IRagChunkStore store,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
    private static readonly (string FileName, string DocumentId)[] CorpusManifest =
    [
        ("01-metadata-completeness-guide.md", "doc-metadata-guide-v1"),
        ("02-print-and-ebook-format-specs.md", "doc-format-specs-v1"),
        ("03-audiobook-production-checklist.md", "doc-audiobook-checklist-v1"),
        ("04-platform-publishing-rules.md", "doc-platform-rules-v1")
    ];

    public async Task UpsertDocumentAsync(
        string documentId,
        string version,
        string markdown,
        int? ownerUserId = null,
        CancellationToken cancellationToken = default)
    {
        var desiredChunks = MarkdownChunker.Chunk(documentId, version, markdown, ownerUserId);
        var existing = await store.GetByDocumentAsync(documentId, ownerUserId, cancellationToken);

        var existingByChunkId = existing.ToDictionary(c => c.ChunkId, StringComparer.Ordinal);
        var desiredChunkIds = desiredChunks.Select(c => c.ChunkId).ToHashSet(StringComparer.Ordinal);

        // STAGE 1: Pre-generate and validate all required embeddings BEFORE mutating or removing any tracked entities.
        // This ensures atomicity even in EF Core InMemory: if generation fails or returns NaN, the database remains completely untouched.
        var generatedEmbeddings = new Dictionary<string, float[]>(StringComparer.Ordinal);
        foreach (var candidate in desiredChunks)
        {
            if (existingByChunkId.TryGetValue(candidate.ChunkId, out var current) &&
                string.Equals(current.Version, version, StringComparison.Ordinal) &&
                string.Equals(current.ContentHash, candidate.ContentHash, StringComparison.Ordinal) &&
                VectorMath.TryValidateEmbedding(current.Embedding))
            {
                continue; // Idempotent: unchanged chunk already embedded
            }

            var vector = await embeddingGenerator.GenerateVectorAsync(
                candidate.Excerpt,
                new EmbeddingGenerationOptions { Dimensions = VectorMath.ExpectedDimensions },
                cancellationToken);
            var floatArray = vector.ToArray();
            VectorMath.ValidateEmbedding(floatArray);
            generatedEmbeddings[candidate.ChunkId] = floatArray;
        }

        // STAGE 2: Apply mutations and removals now that all vector calculations succeeded.
        var toRemove = existing
            .Where(c => !string.Equals(c.Version, version, StringComparison.Ordinal) || !desiredChunkIds.Contains(c.ChunkId))
            .ToList();
        if (toRemove.Count > 0)
            store.RemoveChunks(toRemove);

        var toAdd = new List<RagDocumentChunk>();
        foreach (var candidate in desiredChunks)
        {
            if (existingByChunkId.TryGetValue(candidate.ChunkId, out var current) &&
                string.Equals(current.Version, version, StringComparison.Ordinal))
            {
                if (generatedEmbeddings.TryGetValue(candidate.ChunkId, out var updatedEmbedding))
                {
                    current.Title = candidate.Title;
                    current.Heading = candidate.Heading;
                    current.ChunkIndex = candidate.ChunkIndex;
                    current.Excerpt = candidate.Excerpt;
                    current.ContentHash = candidate.ContentHash;
                    current.Embedding = updatedEmbedding;
                }
            }
            else
            {
                candidate.Embedding = generatedEmbeddings.TryGetValue(candidate.ChunkId, out var newEmbedding)
                    ? newEmbedding
                    : throw new InvalidOperationException($"Missing generated embedding for chunk '{candidate.ChunkId}'.");
                candidate.CreatedAt = DateTime.UtcNow;
                toAdd.Add(candidate);
            }
        }

        if (toAdd.Count > 0)
            store.AddChunks(toAdd);

        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task SeedLockedCorpusAsync(string corpusDirectory, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(corpusDirectory))
            throw new DirectoryNotFoundException($"RAG corpus directory not found: '{corpusDirectory}'.");

        foreach (var (fileName, documentId) in CorpusManifest)
        {
            var fullPath = Path.Combine(corpusDirectory, fileName);
            var markdown = await File.ReadAllTextAsync(fullPath, cancellationToken);
            await UpsertDocumentAsync(documentId, "v1", markdown, ownerUserId: null, cancellationToken);
        }
    }
}
