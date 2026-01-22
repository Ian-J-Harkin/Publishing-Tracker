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
        var platformsGroup = app.MapGroup("/api/platforms").RequireAuthorization();

        platformsGroup.MapGet("/", async ([FromServices] PublishingTrackerDbContext db, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PlatformEndpoints");
            logger.LogInformation("Fetching all platforms.");
            var platforms = await db.Platforms
                .Select(p => new PlatformDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    BaseUrl = p.BaseUrl!,
                    CommissionRate = p.CommissionRate ?? 0
                })
                .ToListAsync();
            return Results.Ok(platforms);
        });

        platformsGroup.MapPost("/requests", async ([FromServices] PublishingTrackerDbContext db, PlatformRequestDto requestDto, HttpContext httpContext, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PlatformEndpoints");
            if (!TryGetUserId(httpContext, out var userId))
            {
                logger.LogWarning("Could not retrieve user ID from token when creating a platform request.");
                return Results.Unauthorized();
            }

            // Ensure user exists in the current database before inserting PlatformRequest
            var user = await db.Users.FindAsync(userId);
            if (user == null)
            {
                logger.LogWarning("User with Id {UserId} not found in database when creating platform request.", userId);
                return Results.Unauthorized();
            }

            var platformRequest = new PlatformRequest
            {
                Name = requestDto.Name,
                BaseUrl = requestDto.BaseUrl,
                CommissionRate = requestDto.CommissionRate,
                UserId = userId
            };

            db.PlatformRequests.Add(platformRequest);
            await db.SaveChangesAsync();

            logger.LogInformation("User {UserId} created a new platform request for {PlatformName}.", userId, requestDto.Name);
            return Results.Created($"/api/platforms/requests/{platformRequest.Id}", platformRequest);
        })
        .WithName("RequestPlatform")
        .WithOpenApi();
    }

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

    private static IDictionary<string, string[]> ValidateModel(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, true);

        return results
            .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : new[] { string.Empty })
                .Select(member => new { member, r.ErrorMessage }))
            .GroupBy(x => x.member)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage ?? string.Empty).ToArray());
    }
}