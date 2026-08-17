using Microsoft.AspNetCore.Mvc;
using MediatR;
using PublishingTracker.Api.Features.Users.Commands;
using PublishingTracker.Api.Features.Users.Queries;

namespace PublishingTracker.Api.Features.Users;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var usersGroup = app.MapGroup("/api/users")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        // --- GET ALL USERS (Admin only) ---
        usersGroup.MapGet("/", async ([FromServices] IMediator mediator) =>
        {
            var result = await mediator.Send(new GetUsersQuery());

            if (!result.IsSuccess)
                return Results.BadRequest(result.ErrorMessage);

            return Results.Ok(result.Value);
        });

        // --- PROMOTE USER TO ADMIN ---
        usersGroup.MapPut("/{id:int}/promote", async ([FromServices] IMediator mediator, int id) =>
        {
            var result = await mediator.Send(new PromoteUserCommand(id));

            if (!result.IsSuccess)
                return Results.BadRequest(result.ErrorMessage);

            return Results.NoContent();
        });

        // --- DEMOTE ADMIN TO USER ---
        usersGroup.MapPut("/{id:int}/demote", async ([FromServices] IMediator mediator, int id) =>
        {
            var result = await mediator.Send(new DemoteUserCommand(id));

            if (!result.IsSuccess)
                return Results.BadRequest(result.ErrorMessage);

            return Results.NoContent();
        });
    }
}
