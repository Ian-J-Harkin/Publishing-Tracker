using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;
using System.Security.Claims;

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
            var query = db.Platforms.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(term));
            }

            var platforms = await query
                .OrderBy(p => p.Name)
                .Select(p => new PlatformResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    BaseUrl = p.BaseUrl ?? string.Empty,
                    CommissionRate = p.CommissionRate,
                    IsActive = p.IsActive
                })
                .ToListAsync();

            return Results.Ok(platforms);
        });

        // --- CREATE NEW PLATFORM (Replacing the "Requests" table) ---
        platformsGroup.MapPost("/", async (PublishingTrackerDbContext db, PlatformRequestDto requestDto, HttpContext httpContext) =>
        {
            // Security precaution: Identify the user
            if (!TryGetUserId(httpContext, out var userId))
            {
                return Results.Unauthorized();
            }

            // Create the actual Database Entity from the DTO
            var platform = new Platform
            {
                Name = requestDto.Name,
                BaseUrl = requestDto.BaseUrl,
                CommissionRate = requestDto.CommissionRate,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
                // UserId = userId // Include this if you add UserId to your Platform table
            };

            db.Platforms.Add(platform);
            await db.SaveChangesAsync();

            // Return the Response DTO (clean data for the React frontend)
            var response = new PlatformResponseDto
            {
                Id = platform.Id,
                Name = platform.Name,
                BaseUrl = platform.BaseUrl ?? string.Empty,
                CommissionRate = platform.CommissionRate,
                IsActive = platform.IsActive
            };

            return Results.Created($"/api/platforms/{platform.Id}", response);
        })
        .WithName("CreatePlatform")
        .WithOpenApi();
    }

    // Helper stays here to serve the POST method
    private static bool TryGetUserId(HttpContext httpContext, out int userId)
    {
        var userIdClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out userId))
        {
            return true;
        }

        userId = 0;
        return false;
    }
}