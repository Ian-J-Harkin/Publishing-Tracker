using Microsoft.AspNetCore.Mvc;
using PublishingTracker.Domain.Dtos;
using MediatR;
using PublishingTracker.Domain.Dtos;
using PublishingTracker.Api.Features.Auth.Commands;
using PublishingTracker.Api.Features.Auth.Queries;

namespace PublishingTracker.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (IMediator mediator, RegisterRequest request, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("AuthEndpoints");
            var command = new RegisterUserCommand(request.Email, request.Password, request.FirstName, request.LastName);
            var authResponse = await mediator.Send(command);
            
            if (authResponse == null)
            {
                logger.LogWarning("Registration failed for email {Email}: Email already exists.", request.Email);
                return Results.BadRequest("Email already exists.");
            }
            logger.LogInformation("User registered successfully with email {Email}.", request.Email);
            return Results.Ok(authResponse);
        });

        app.MapPost("/api/auth/login", async (IMediator mediator, LoginRequest request, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("AuthEndpoints");
            var query = new LoginUserQuery(request.Email, request.Password);
            var authResponse = await mediator.Send(query);
            
            if (authResponse == null)
            {
                logger.LogWarning("Login failed for email {Email}.", request.Email);
                return Results.Unauthorized();
            }
            logger.LogInformation("User logged in successfully with email {Email}.", request.Email);
            return Results.Ok(authResponse);
        });
    }
}