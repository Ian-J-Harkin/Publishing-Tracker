using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;

namespace PublishingTracker.Api.Extensions;

public static class PlatformRoutes
{
    public static void MapPlatformRoutes(this WebApplication app)
    {
        var platformsGroup = app.MapGroup("/api/platforms").RequireAuthorization();

        platformsGroup.MapGet("/", async ([FromServices] PublishingTrackerDbContext db) =>
        {
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

        platformsGroup.MapPost("/requests", async ([FromServices] PublishingTrackerDbContext db, PlatformRequestDto requestDto, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);

            var platformRequest = new PlatformRequest
            {
                Name = requestDto.Name,
                BaseUrl = requestDto.BaseUrl,
                CommissionRate = requestDto.CommissionRate,
                UserId = userId
            };

            db.PlatformRequests.Add(platformRequest);
            await db.SaveChangesAsync();

            return Results.Created($"/api/platforms/requests/{platformRequest.Id}", platformRequest);
        })
        .WithName("RequestPlatform")
        .WithOpenApi();
    }
}