using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Common.Extensions;
using PublishingTracker.Domain;
using Microsoft.AspNetCore.Mvc;
using PublishingTracker.Api.Extensions;
using MediatR;
using PublishingTracker.Application.Users.Commands;
using PublishingTracker.Application.Users.Queries;

namespace PublishingTracker.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var profileGroup = app.MapGroup("/api/users/me").RequireAuthorization();

        profileGroup.MapGet("/", async ([FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new GetProfileQuery());
            if (!result.IsSuccess)
                return result.ErrorMessage == "Unauthorized"
                    ? ApiResults.Unauthorized("Unauthorized")
                    : ApiResults.NotFound(result.ErrorMessage ?? "Not found");
            return Results.Ok(result.Value);
        });

        profileGroup.MapPut("/profile", async ([FromServices] IMediator mediator, UpdateProfileCommand command) =>
        {
            var result = await mediator.Send(command);
            if (!result.IsSuccess)
                return result.ErrorMessage == "Unauthorized"
                    ? ApiResults.Unauthorized("Unauthorized")
                    : ApiResults.BadRequest(result.ErrorMessage ?? "Request failed");
            return Results.Ok();
        });

        profileGroup.MapPut("/password", async ([FromServices] IMediator mediator, ChangePasswordCommand command) =>
        {
            var result = await mediator.Send(command);
            if (!result.IsSuccess)
                return result.ErrorMessage == "Unauthorized"
                    ? ApiResults.Unauthorized("Unauthorized")
                    : ApiResults.BadRequest(result.ErrorMessage ?? "Request failed");
            return Results.Ok();
        });

        var usersGroup = app.MapGroup("/api/users")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        // --- GET ALL USERS (Admin only) ---
        usersGroup.MapGet("/", async ([FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new GetUsersQuery());

            if (!result.IsSuccess)
                return ApiResults.BadRequest(result.ErrorMessage ?? "Request failed");

            return Results.Ok(result.Value);
        });

        // --- PROMOTE USER TO ADMIN ---
        usersGroup.MapPut("/{id:int}/promote", async ([FromServices] IMediator mediator, int id) =>
        {
            var result = await mediator.Send(new PromoteUserCommand(id));

            if (!result.IsSuccess)
                return ApiResults.BadRequest(result.ErrorMessage ?? "Request failed");

            return Results.NoContent();
        });

        // --- DEMOTE ADMIN TO USER ---
        usersGroup.MapPut("/{id:int}/demote", async ([FromServices] IMediator mediator, int id) =>
        {
            var result = await mediator.Send(new DemoteUserCommand(id));

            if (!result.IsSuccess)
                return ApiResults.BadRequest(result.ErrorMessage ?? "Request failed");

            return Results.NoContent();
        });
    }
}
