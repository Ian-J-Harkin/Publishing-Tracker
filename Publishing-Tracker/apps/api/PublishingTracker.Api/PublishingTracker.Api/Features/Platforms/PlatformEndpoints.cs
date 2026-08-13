using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Api.Models;
using PublishingTracker.Domain;
using PublishingTracker.Domain.Dtos;

namespace PublishingTracker.Api.Features.Platforms;

public static class PlatformEndpoints
{
    public static void MapPlatformEndpoints(this WebApplication app)
    {
        // RequireAuthorization ensures a valid JWT is present
        var platformsGroup = app.MapGroup("/api/platforms").RequireAuthorization();

        // --- GET ALL PLATFORMS ---
        platformsGroup.MapGet("/", async (PublishingTrackerDbContext db, [FromServices] IMemoryCache cache, [FromQuery] string? search) =>
        {
            var cacheKey = string.IsNullOrWhiteSpace(search) ? "platforms_all" : $"platforms_search_{search.ToLower()}";

            var platforms = await cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60);

                return await db.Platforms
                    .Search(search)
                    .OrderBy(p => p.Name)
                    .Select(p => p.ToResponseDto())
                    .ToListAsync();
            });

            return Results.Ok(platforms);
        });

        // --- CREATE NEW PLATFORM ---
        platformsGroup.MapPost("/", async (PublishingTrackerDbContext db, [FromServices] IMemoryCache cache, PlatformRequestDto requestDto, HttpContext httpContext) =>
        {
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            var platform = new Platform
            {
                Name = requestDto.Name,
                BaseUrl = requestDto.BaseUrl,
                CommissionRate = requestDto.CommissionRate,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            db.Platforms.Add(platform);
            await db.SaveChangesAsync();

            // Invalidate the cache to ensure the new platform appears in subsequent requests
            // While we can't easily invalidate all search caches, we MUST invalidate the primary one.
            cache.Remove("platforms_all");

            return Results.Created($"/api/platforms/{platform.Id}", platform.ToResponseDto());
        })
        .WithName("CreatePlatform")
        .WithOpenApi();
    }
}