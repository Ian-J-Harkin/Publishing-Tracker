using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;

namespace PublishingTracker.Api.Features.Platforms;

public static class PlatformEndpoints
{
    public static void MapPlatformEndpoints(this WebApplication app)
    {
        // RequireAuthorization ensures a valid JWT is present
        var platformsGroup = app.MapGroup("/api/platforms").RequireAuthorization();

        // --- GET ALL PLATFORMS ---
        platformsGroup.MapGet("/", async (PublishingTrackerDbContext db, [FromQuery] string? search) =>
        {
            var platforms = await db.Platforms
                .Search(search)
                .OrderBy(p => p.Name)
                .Select(p => p.ToResponseDto())
                .ToListAsync();

            return Results.Ok(platforms);
        });

        // --- CREATE NEW PLATFORM ---
        platformsGroup.MapPost("/", async (PublishingTrackerDbContext db, PlatformRequestDto requestDto, HttpContext httpContext) =>
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

            return Results.Created($"/api/platforms/{platform.Id}", platform.ToResponseDto());
        })
        .WithName("CreatePlatform")
        .WithOpenApi();
    }
}