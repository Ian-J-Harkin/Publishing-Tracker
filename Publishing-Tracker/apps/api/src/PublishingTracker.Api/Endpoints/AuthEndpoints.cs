using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Common.Extensions;
using PublishingTracker.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Application.Common.Dtos;
using MediatR;
using PublishingTracker.Application.Auth.Commands;
using PublishingTracker.Application.Auth.Queries;

namespace PublishingTracker.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (IMediator mediator, RegisterRequest request, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("AuthEndpoints");
            var command = new RegisterUserCommand(request.Email, request.Password, request.FirstName, request.LastName);
            var result = await mediator.Send(command);
            if (!result.IsSuccess)
            {
                logger.LogWarning("Registration failed for email {Email}: {Error}", request.Email, result.ErrorMessage);
                return ApiResults.BadRequest(result.ErrorMessage ?? "Registration failed");
            }

            logger.LogInformation("User registered successfully with email {Email}.", request.Email);
            return Results.Ok(result.Value);
        });

        app.MapPost("/api/auth/login", async (IMediator mediator, LoginRequest request, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("AuthEndpoints");
            var query = new LoginUserQuery(request.Email, request.Password);
            var result = await mediator.Send(query);

            if (!result.IsSuccess)
            {
                logger.LogWarning("Login failed for email {Email}: {Error}", request.Email, result.ErrorMessage);
                return ApiResults.Unauthorized(result.ErrorMessage ?? "Invalid email or password.");
            }
            logger.LogInformation("User logged in successfully with email {Email}.", request.Email);
            return Results.Ok(result.Value);
        });
    }
}
