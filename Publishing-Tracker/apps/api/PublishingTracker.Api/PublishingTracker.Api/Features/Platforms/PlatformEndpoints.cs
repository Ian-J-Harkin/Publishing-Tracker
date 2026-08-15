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
        // RequireAuthorization ensures a valid JWT is present
        var platformsGroup = app.MapGroup("/api/platforms").RequireAuthorization();

        // --- GET ALL PLATFORMS ---
        platformsGroup.MapGet("/", async ([FromServices] IMediator mediator, [FromQuery] string? search) =>
        {
            var query = new GetPlatformsQuery(search);
            var result = await mediator.Send(query);
            
            if (!result.IsSuccess)
                return result.ErrorMessage == "Unauthorized" ? Results.Unauthorized() : Results.BadRequest(result.ErrorMessage);
                
            return Results.Ok(result.Value);
        });

        // --- CREATE NEW PLATFORM ---
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
    }
}