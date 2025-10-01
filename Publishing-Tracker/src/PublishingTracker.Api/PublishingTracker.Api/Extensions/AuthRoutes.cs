using Microsoft.AspNetCore.Mvc;
using PublishingTracker.Api.Models.Dtos;
using PublishingTracker.Api.Services;

namespace PublishingTracker.Api.Extensions;

public static class AuthRoutes
{
    public static void MapAuthRoutes(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (IAuthService authService, RegisterRequest request) =>
        {
            var authResponse = await authService.RegisterAsync(request);
            return authResponse == null ? Results.BadRequest("Email already exists.") : Results.Ok(authResponse);
        });

        app.MapPost("/api/auth/login", async (IAuthService authService, LoginRequest request) =>
        {
            var authResponse = await authService.LoginAsync(request);
            if (authResponse == null)
            {
                return Results.Unauthorized();
            }
            return authResponse == null ? Results.Unauthorized() : Results.Ok(authResponse);
        });
    }
}