using PublishingTracker.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
namespace PublishingTracker.Domain.Entities;

public enum EditionFormat
{
    Ebook,
    Paperback,
    Hardcover,
    Audiobook,
    LargeText
}

public class Edition : IEntity
{
    public int Id { get; private set; }
    public int BookId { get; private set; }
    public EditionFormat Format { get; private set; }
    public string? ISBN { get; private set; }
    public DateTime? PublicationDate { get; private set; }
    public decimal? BasePrice { get; private set; }
    public int? PageCount { get; private set; }
    public string? Dimensions { get; private set; }
    public decimal? FileSize { get; private set; }
    public string? Narrator { get; private set; }
    public TimeSpan? Duration { get; private set; }

    [Auditable(isCreationTime: true)]
    public DateTime CreatedAt { get; private set; }

    [Auditable]
    public DateTime UpdatedAt { get; private set; }

    // Navigation properties
    public Book Book { get; private set; } = null!;
    public List<PlatformEditionMetadata> PlatformMetadata { get; private set; } = new();
    public List<Sale> Sales { get; private set; } = new();

    private Edition() { }

    public Edition(int bookId, EditionFormat format, string? isbn = null,
        DateTime? publicationDate = null, decimal? basePrice = null,
        int? pageCount = null, string? dimensions = null, decimal? fileSize = null,
        string? narrator = null, TimeSpan? duration = null)
    {
        BookId = bookId;
        Format = format;
        ISBN = isbn;
        PublicationDate = NormalizeUtc(publicationDate);
        BasePrice = basePrice;
        PageCount = pageCount;
        Dimensions = dimensions;
        FileSize = fileSize;
        Narrator = narrator;
        Duration = duration;
    }

    public void Update(string? isbn, DateTime? publicationDate, decimal? basePrice,
        int? pageCount = null, string? dimensions = null, decimal? fileSize = null,
        string? narrator = null, TimeSpan? duration = null)
    {
        ISBN = isbn;
        PublicationDate = NormalizeUtc(publicationDate);
        BasePrice = basePrice;
        PageCount = pageCount;
        Dimensions = dimensions;
        FileSize = fileSize;
        Narrator = narrator;
        Duration = duration;
    }

    private static DateTime? NormalizeUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } dt => dt,
        { Kind: DateTimeKind.Local } dt => dt.ToUniversalTime(),
        var dt => DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc)
    };

    public void UpdatePricing(decimal newPrice)
    {
        if (newPrice < 0) throw new InvalidDomainStateException("Price cannot be negative.");
        BasePrice = newPrice;
    }
}