using PublishingTracker.Domain.Entities;
using PublishingTracker.Infrastructure.Persistence;

namespace PublishingTracker.Api.Tests;

public sealed record SeedCatalogUsers(User UserA, User UserB, Platform DigitalPlatform);

/// <summary>
/// Deterministic, non-PII two-user test fixture covering complete titles, format-specific missing metadata,
/// free promos (BasePrice == 0m), zero-edition books, and cross-user isolation boundaries.
/// </summary>
public static class TwoUserSeedCatalog
{
    public static async Task<SeedCatalogUsers> SeedAsync(PublishingTrackerDbContext dbContext)
    {
        var userA = User.Create("author-a@example.test", "synthetic-hash-a", "Author", "Alpha", "User");
        var userB = User.Create("author-b@example.test", "synthetic-hash-b", "Author", "Beta", "User");
        var platform = Platform.Create("Kindle Direct", "https://kdp.example.test", 0.30m);
        platform.Approve();

        dbContext.Users.AddRange(userA, userB);
        dbContext.Platforms.Add(platform);
        await dbContext.SaveChangesAsync();

        var pubDate = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        // User A — Book 1: Complete title (Print ISBN + Ebook PlatformProductId) -> must NOT report missing metadata
        var completeBook = new Book(
            userA.Id,
            "Complete Catalogue Title",
            "Author Alpha",
            seriesName: "Harbor Tales",
            seriesNumber: 1,
            genre: "Mystery",
            description: "A complete mystery novel.",
            coverImageUrl: "https://cdn.example.test/complete.jpg");
        completeBook.AddEdition(EditionFormat.Paperback, isbn: "9780000000001", publicationDate: pubDate, basePrice: 14.99m, pageCount: 310, dimensions: "5.5 x 8.5 in");
        var completeEbook = completeBook.AddEdition(EditionFormat.Ebook, isbn: null, publicationDate: pubDate, basePrice: 4.99m, fileSize: 2.4m);

        // User A — Book 2: Missing Description, CoverImageUrl, and SeriesNumber (SeriesName is set)
        var missingBookFields = new Book(
            userA.Id,
            "Clockwork Missing Book Metadata",
            "Author Alpha",
            seriesName: "Clockwork Isles",
            seriesNumber: null,
            genre: "Fantasy",
            description: null,
            coverImageUrl: "   ");
        missingBookFields.AddEdition(EditionFormat.Hardcover, isbn: "9780000000002", publicationDate: pubDate, basePrice: 24.00m, pageCount: 400, dimensions: "6 x 9 in");

        // User A — Book 3: Audiobook missing Narrator & Duration (has PlatformProductId, no ISBN -> valid identifier)
        var incompleteAudioBook = new Book(
            userA.Id,
            "Audio Missing Narrator",
            "Author Alpha",
            genre: "Sci-Fi",
            description: "Audio edition lacking narrator credit.",
            coverImageUrl: "https://cdn.example.test/audio.jpg");
        var audioEdition = incompleteAudioBook.AddEdition(EditionFormat.Audiobook, isbn: null, publicationDate: pubDate, basePrice: 19.99m);

        // User A — Book 4: Free promo Ebook (BasePrice = 0.00m is valid, not missing)
        var freePromoBook = new Book(
            userA.Id,
            "Free Promo Ebook",
            "Author Alpha",
            genre: "Fiction",
            description: "Free reader magnet.",
            coverImageUrl: "https://cdn.example.test/promo.jpg");
        var freeEbook = freePromoBook.AddEdition(EditionFormat.Ebook, isbn: "9780000000004", publicationDate: pubDate, basePrice: 0.00m, fileSize: 1.8m);

        // User A — Book 5: Zero editions
        var zeroEditionBook = new Book(
            userA.Id,
            "Draft With No Editions",
            "Author Alpha",
            genre: "Thriller",
            description: "Not yet formatted.",
            coverImageUrl: "https://cdn.example.test/draft.jpg");

        // User B — Book 6: Incomplete Hardcover owned by User B (isolation target)
        var userBBook = new Book(
            userB.Id,
            "Beta Private Incomplete Hardcover",
            "Author Beta",
            genre: "History",
            description: "Private User B title.",
            coverImageUrl: "https://cdn.example.test/beta.jpg");
        userBBook.AddEdition(EditionFormat.Hardcover, isbn: null, publicationDate: pubDate, basePrice: 29.99m, pageCount: null, dimensions: null);

        dbContext.Books.AddRange(completeBook, missingBookFields, incompleteAudioBook, freePromoBook, zeroEditionBook, userBBook);
        await dbContext.SaveChangesAsync();

        dbContext.PlatformEditionMetadata.AddRange(
            new PlatformEditionMetadata(completeEbook.Id, platform.Id, platformProductId: "B0COMPLETE01", isPublished: true),
            new PlatformEditionMetadata(audioEdition.Id, platform.Id, platformProductId: "B0AUDIO0001", isPublished: false),
            new PlatformEditionMetadata(freeEbook.Id, platform.Id, platformProductId: "B0FREE00001", isPublished: true));
        await dbContext.SaveChangesAsync();

        return new SeedCatalogUsers(userA, userB, platform);
    }
}
