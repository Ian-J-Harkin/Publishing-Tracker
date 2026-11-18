using PublishingTracker.Domain.Exceptions;

namespace PublishingTracker.Domain.Entities;

public class PlatformEditionMetadata : IEntity
{
    public int Id { get; private set; }
    public int EditionId { get; private set; }
    public int PlatformId { get; private set; }

    // Common identifiers
    public string? PlatformProductId { get; private set; } // ASIN, Kobo ID, Apple ID, etc.
    public string? ProductUrl { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTime? PublishedDate { get; private set; }

    // Platform-specific metadata stored as JSON
    public string MetadataJson { get; private set; } = "{}";

    [Auditable(isCreationTime: true)]
    public DateTime CreatedAt { get; private set; }

    [Auditable]
    public DateTime UpdatedAt { get; private set; }

    // Navigation properties
    public Edition Edition { get; private set; } = null!;
    public Platform Platform { get; private set; } = null!;

    private PlatformEditionMetadata() { }

    public PlatformEditionMetadata(int editionId, int platformId, 
        string? platformProductId = null, string? productUrl = null, 
        bool isPublished = false, string metadataJson = "{}")
    {
        EditionId = editionId;
        PlatformId = platformId;
        PlatformProductId = platformProductId;
        ProductUrl = productUrl;
        IsPublished = isPublished;
        MetadataJson = metadataJson;
    }

    public void UpdateMetadata(string? platformProductId, string? productUrl, 
        bool isPublished, DateTime? publishedDate, string metadataJson)
    {
        PlatformProductId = platformProductId;
        ProductUrl = productUrl;
        IsPublished = isPublished;
        PublishedDate = NormalizeUtc(publishedDate);
        MetadataJson = metadataJson;
    }

    public void Publish(DateTime? publishedDate = null)
    {
        IsPublished = true;
        PublishedDate = NormalizeUtc(publishedDate) ?? DateTime.UtcNow;
    }

    public void Unpublish()
    {
        IsPublished = false;
    }

    private static DateTime? NormalizeUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } dt => dt,
        { Kind: DateTimeKind.Local } dt => dt.ToUniversalTime(),
        var dt => DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc)
    };
}