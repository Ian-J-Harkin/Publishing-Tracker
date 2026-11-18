using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using PublishingTracker.Application.Books.Services;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Infrastructure.Authentication;
using PublishingTracker.Infrastructure.Persistence;

namespace PublishingTracker.Api.Tests;

public class Phase0BaselineTests
{
    [Fact]
    public void TokenService_EmitsIntegerIatAndConfiguredUtcExpiry()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "phase0-unit-test-signing-key-at-least-32-chars",
                ["Jwt:Issuer"] = "PublishingTracker.Api",
                ["Jwt:Audience"] = "PublishingTracker.Api",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

        var service = new TokenService(config);
        var user = User.Create("author-a@example.test", "hash", "Author", "Alpha", "User");

        var rawJwt = service.CreateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(rawJwt);

        var iatClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat);
        Assert.NotNull(iatClaim);
        Assert.Equal(ClaimValueTypes.Integer64, iatClaim!.ValueType);
        Assert.True(long.TryParse(iatClaim.Value, out var iatSeconds));

        var issuedAtUtc = DateTimeOffset.FromUnixTimeSeconds(iatSeconds).UtcDateTime;
        var lifetime = jwt.ValidTo - issuedAtUtc;
        Assert.InRange(lifetime.TotalMinutes, 59.5, 60.5);
    }

    [Fact]
    public async Task FormatCompletenessRules_EvaluatesBookAndFormatSpecificRulesCorrectly()
    {
        var options = new DbContextOptionsBuilder<PublishingTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new PublishingTrackerDbContext(options);
        var seeded = await TwoUserSeedCatalog.SeedAsync(db);

        var userABooks = await db.Books
            .Where(b => b.UserId == seeded.UserA.Id)
            .Include(b => b.Editions)
                .ThenInclude(e => e.PlatformMetadata)
            .OrderBy(b => b.Title)
            .ToListAsync();

        var evals = userABooks.ToDictionary(b => b.Title, FormatCompletenessRules.EvaluateBook);

        // 1. Complete title (Print ISBN + Ebook PlatformProductId) -> complete
        Assert.False(evals["Complete Catalogue Title"].IsIncomplete);

        // 2. Book missing Description, CoverImageUrl, and conditional SeriesNumber
        var bookMissing = evals["Clockwork Missing Book Metadata"];
        Assert.True(bookMissing.IsIncomplete);
        Assert.Equal(["Description", "CoverImageUrl", "SeriesNumber"], bookMissing.MissingBookFields);

        // 3. Audiobook with PlatformProductId (no ISBN required) missing Narrator & Duration
        var audio = evals["Audio Missing Narrator"];
        Assert.True(audio.IsIncomplete);
        Assert.Empty(audio.MissingBookFields);
        Assert.Equal(["Narrator", "Duration"], audio.Editions.Single().MissingFields);

        // 4. Free promo Ebook (BasePrice = 0.00m) -> complete
        Assert.False(evals["Free Promo Ebook"].IsIncomplete);

        // 5. Book with 0 editions -> missing Editions
        var zeroEditions = evals["Draft With No Editions"];
        Assert.True(zeroEditions.IsIncomplete);
        Assert.Equal(["Editions"], zeroEditions.MissingBookFields);
    }

    [Fact]
    public async Task TwoUserSeedCatalog_OwnerScopedQuery_IsolatesUserAFromUserB()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<PublishingTrackerDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        int userAId;
        await using (var setupDb = new PublishingTrackerDbContext(options))
        {
            var seeded = await TwoUserSeedCatalog.SeedAsync(setupDb);
            userAId = seeded.UserA.Id;
        }

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(u => u.UserId).Returns(userAId);

        await using var scopedDb = new PublishingTrackerDbContext(options, currentUser.Object);

        var visibleBooks = await scopedDb.Books.Where(b => b.UserId == userAId).ToListAsync();
        var visibleEditions = await scopedDb.Editions.ToListAsync();

        Assert.Equal(5, visibleBooks.Count);
        Assert.DoesNotContain(visibleBooks, b => b.Title == "Beta Private Incomplete Hardcover");
        Assert.DoesNotContain(visibleEditions, e => e.Book.Title == "Beta Private Incomplete Hardcover");
    }
}
