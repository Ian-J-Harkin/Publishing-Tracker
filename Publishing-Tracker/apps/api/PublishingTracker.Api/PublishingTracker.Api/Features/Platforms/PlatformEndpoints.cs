using Microsoft.AspNetCore.Mvc;
using PublishingTracker.Domain.Dtos;
using PublishingTracker.Application.Platforms.Queries;
using PublishingTracker.Application.Platforms.Commands;
using MediatR;

namespace PublishingTracker.Api.Features.Platforms;

public static class PlatformEndpoints
{
    public static void MapPlatformEndpoints(this WebApplication app)
    {
        var platformsGroup = app.MapGroup("/api/platforms").RequireAuthorization();

        // --- GET ALL PLATFORMS ---
        // Standard users see only active platforms. Admins can pass ?includeInactive=true.
        platformsGroup.MapGet("/", async ([FromServices] IMediator mediator, [FromQuery] string? search, [FromQuery] bool includeInactive = false) =>
        {
            var query = new GetPlatformsQuery(search, includeInactive);
            var result = await mediator.Send(query);
            
            if (!result.IsSuccess)
                return result.ErrorMessage == "Unauthorized" ? Results.Unauthorized() : Results.BadRequest(result.ErrorMessage);
                
            return Results.Ok(result.Value);
        });

        // --- CREATE NEW PLATFORM ---
        // Platforms are created as inactive (pending approval) by default.
        platformsGroup.MapPost("/", async ([FromServices] IMediator mediator, PlatformRequestDto requestDto) =>
        {
            var command = new CreatePlatformCommand(requestDto.Name, requestDto.BaseUrl, requestDto.CommissionRate);
            var result = await mediator.Send(command);
            
            if (!result.IsSuccess)
                return result.ErrorMessage == "Unauthorized" ? Results.Unauthorized() : Results.BadRequest(result.ErrorMessage);

            return Results.Created($"/api/platforms/{result.Value?.Id}", result.Value);
        })
        .WithName("CreatePlatform")
        .WithOpenApi();

        // --- APPROVE PLATFORM (Admin only) ---
        platformsGroup.MapPut("/{id:int}/approve", async ([FromServices] IMediator mediator, int id) =>
        {
            var result = await mediator.Send(new ApprovePlatformCommand(id));

            if (!result.IsSuccess)
                return Results.BadRequest(result.ErrorMessage);

            return Results.NoContent();
        })
        .RequireAuthorization(policy => policy.RequireRole("Admin"));
    }
}