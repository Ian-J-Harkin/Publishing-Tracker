using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Rag.Entities;
using PublishingTracker.Rag.Services;

namespace PublishingTracker.Infrastructure.Persistence;

/// <summary>
/// Idempotent development seeder providing authenticatable demo accounts,
/// the canonical two-user catalogue matrix, and baseline RAG guideline/note chunks.
/// </summary>
public static class DevelopmentCatalogSeeder
{
    public const string DemoPassword = "Password123!";

    public static async Task SeedAsync(
        PublishingTrackerDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        User? userA = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == "author-a@example.test", cancellationToken);
        User? userB = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == "author-b@example.test", cancellationToken);

        if (userA is null)
        {
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(
                DemoPassword,
                BCrypt.Net.BCrypt.GenerateSalt(),
                enhancedEntropy: false,
                hashType: BCrypt.Net.HashType.SHA256);

            userA = User.Create("author-a@example.test", passwordHash, "Author", "Alpha", "User");
            userB ??= User.Create("author-b@example.test", passwordHash, "Author", "Beta", "User");

            var platform = await dbContext.Platforms
                .FirstOrDefaultAsync(p => p.Name == "Kindle Direct", cancellationToken);
            if (platform is null)
            {
                platform = Platform.Create("Kindle Direct", "https://kdp.example.test", 0.30m);
                platform.Approve();
                dbContext.Platforms.Add(platform);
            }

            dbContext.Users.Add(userA);
            if (dbContext.Entry(userB).State == EntityState.Detached)
            {
                dbContext.Users.Add(userB);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var pubDate = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc);

            // User A — Book 1: Complete title (Print ISBN + Ebook PlatformProductId)
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

            // User A — Book 3: Audiobook missing Narrator & Duration (has PlatformProductId)
            var incompleteAudioBook = new Book(
                userA.Id,
                "Audio Missing Narrator",
                "Author Alpha",
                genre: "Sci-Fi",
                description: "Audio edition lacking narrator credit.",
                coverImageUrl: "https://cdn.example.test/audio.jpg");
            var audioEdition = incompleteAudioBook.AddEdition(EditionFormat.Audiobook, isbn: null, publicationDate: pubDate, basePrice: 19.99m);

            // User A — Book 4: Free promo Ebook (BasePrice = 0.00m is valid)
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

            // User B — Book 6: Incomplete Hardcover owned exclusively by User B
            var userBBook = new Book(
                userB.Id,
                "Beta Private Incomplete Hardcover",
                "Author Beta",
                genre: "History",
                description: "Private User B title.",
                coverImageUrl: "https://cdn.example.test/beta.jpg");
            userBBook.AddEdition(EditionFormat.Hardcover, isbn: null, publicationDate: pubDate, basePrice: 29.99m, pageCount: null, dimensions: null);

            dbContext.Books.AddRange(completeBook, missingBookFields, incompleteAudioBook, freePromoBook, zeroEditionBook, userBBook);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.PlatformEditionMetadata.AddRange(
                new PlatformEditionMetadata(completeEbook.Id, platform.Id, platformProductId: "B0COMPLETE01", isPublished: true),
                new PlatformEditionMetadata(audioEdition.Id, platform.Id, platformProductId: "B0AUDIO0001", isPublished: false),
                new PlatformEditionMetadata(freeEbook.Id, platform.Id, platformProductId: "B0FREE00001", isPublished: true));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.RagDocumentChunks.AnyAsync(cancellationToken))
        {
            var chunks = new List<RagDocumentChunk>
            {
                BuildSeedChunk(
                    "doc-metadata-guide-v1",
                    "PublishingTracker Metadata Completeness Guide",
                    "Book and Edition Required Fields",
                    0,
                    "All books require non-empty Description, Genre, CoverImageUrl, and at least one Edition. SeriesNumber is required whenever SeriesName is non-empty. BasePrice 0.00 is valid for free promos.",
                    ownerUserId: null,
                    axis: 0),
                BuildSeedChunk(
                    "doc-format-specs-v1",
                    "Print and Ebook Format Specifications",
                    "Ebook and Print Validation Rules",
                    0,
                    "Print editions require ISBN, PageCount > 0, and non-empty physical Dimensions. Ebooks require FileSize > 0, PublicationDate, BasePrice, and either an ISBN or a non-empty PlatformProductId (such as a Kindle ASIN).",
                    ownerUserId: null,
                    axis: 1),
                BuildSeedChunk(
                    "doc-audiobook-checklist-v1",
                    "Audiobook Production Checklist",
                    "Required Narrator, Duration, and Identifiers",
                    0,
                    "Every Audiobook edition requires a non-whitespace Narrator credit and a Duration strictly greater than TimeSpan.Zero, plus either an ISBN or at least one non-empty PlatformProductId.",
                    ownerUserId: null,
                    axis: 2),
                BuildSeedChunk(
                    "doc-platform-rules-v1",
                    "Platform Publishing and Tenant Scope Rules",
                    "PlatformSummaries and Citation Integrity",
                    0,
                    "Publication state (IsPublished, PublishedDate, PlatformProductId) is tracked per platform in PlatformSummaries. PublishingTracker does not maintain historical effective-dated price logs or book-level workflow revision history.",
                    ownerUserId: null,
                    axis: 3),
                BuildSeedChunk(
                    "doc-user-a-audio-note-v1",
                    "Author Alpha Production Note",
                    "Audiobook Narrator Reminder",
                    0,
                    "Personal reminder for Author Alpha: finalize studio narrator assignment and runtime check for Audio Missing Narrator before KDP/ACX submission.",
                    ownerUserId: userA.Id,
                    axis: 4)
            };

            if (userB is not null)
            {
                chunks.Add(BuildSeedChunk(
                    "doc-user-b-private-note-v1",
                    "Author Beta Confidential Release Plan",
                    "Private Hardcover Schedule",
                    0,
                    "Private User B note: confidential print run details for Beta Private Incomplete Hardcover.",
                    ownerUserId: userB.Id,
                    axis: 5));
            }

            dbContext.RagDocumentChunks.AddRange(chunks);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static RagDocumentChunk BuildSeedChunk(
        string documentId,
        string title,
        string heading,
        int chunkIndex,
        string excerpt,
        int? ownerUserId,
        int axis)
    {
        var embedding = new float[VectorMath.ExpectedDimensions];
        embedding[axis % VectorMath.ExpectedDimensions] = 1.0f;

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(excerpt));
        return new RagDocumentChunk
        {
            DocumentId = documentId,
            Version = "seed-v0",
            ChunkId = $"{documentId}#c{chunkIndex}",
            Title = title,
            Heading = heading,
            ChunkIndex = chunkIndex,
            Excerpt = excerpt,
            ContentHash = Convert.ToHexString(hashBytes).ToLowerInvariant(),
            OwnerUserId = ownerUserId,
            Embedding = embedding,
            CreatedAt = DateTime.UtcNow
        };
    }
}
