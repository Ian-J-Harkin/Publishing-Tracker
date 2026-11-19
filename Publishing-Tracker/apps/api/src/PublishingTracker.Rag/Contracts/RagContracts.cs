namespace PublishingTracker.Rag.Contracts;

public sealed record RagChunkHitDto(
    string DocumentId,
    string ChunkId,
    string Title,
    string Excerpt,
    double CosineDistance);

public sealed record RagSearchResultDto(
    string Query,
    int TopK,
    IReadOnlyList<RagChunkHitDto> Hits);
