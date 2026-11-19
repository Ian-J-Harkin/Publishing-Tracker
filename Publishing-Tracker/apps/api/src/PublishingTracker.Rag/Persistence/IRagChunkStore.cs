using PublishingTracker.Rag.Contracts;
using PublishingTracker.Rag.Entities;

namespace PublishingTracker.Rag.Persistence;

public interface IRagChunkStore
{
    bool IsPostgreSql { get; }

    Task<List<RagDocumentChunk>> GetByDocumentAsync(
        string documentId,
        int? ownerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RagChunkHitDto>> SearchByVectorAsync(
        int authenticatedUserId,
        ReadOnlyMemory<float> queryVector,
        int topK,
        CancellationToken cancellationToken = default);

    void AddChunks(IEnumerable<RagDocumentChunk> chunks);
    void RemoveChunks(IEnumerable<RagDocumentChunk> chunks);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
