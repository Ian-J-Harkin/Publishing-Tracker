using Microsoft.AspNetCore.Mvc;
using PublishingTracker.Api.Models.Dtos;
using PublishingTracker.Api.Services;

namespace PublishingTracker.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (IAuthService authService, RegisterRequest request, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("AuthEndpoints");
            var authResponse = await authService.RegisterAsync(request);
            if (authResponse == null)
            {
                logger.LogWarning("Registration failed for email {Email}: Email already exists.", request.Email);
                return Results.BadRequest("Email already exists.");
            }
            logger.LogInformation("User registered successfully with email {Email}.", request.Email);
            return Results.Ok(authResponse);
        });

        app.MapPost("/api/auth/login", async (IAuthService authService, LoginRequest request, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("AuthEndpoints");
            var authResponse = await authService.LoginAsync(request);
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