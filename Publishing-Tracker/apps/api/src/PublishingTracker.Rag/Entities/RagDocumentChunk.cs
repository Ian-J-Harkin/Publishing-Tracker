namespace PublishingTracker.Rag.Entities;

public sealed class RagDocumentChunk
{
    public int Id { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string Version { get; set; } = "v1";
    public string ChunkId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Heading { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Excerpt { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Null for shared repo guidelines; non-null for private caller notes.
    /// Enforced strictly outside LLM control in host retrieval queries.
    /// </summary>
    public int? OwnerUserId { get; set; }

    public float[] Embedding { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
