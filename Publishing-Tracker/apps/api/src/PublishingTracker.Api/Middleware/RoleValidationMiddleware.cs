using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Infrastructure.Persistence;

namespace PublishingTracker.Api.Middleware;

/// <summary>
/// Middleware that checks if the authenticated user's role has changed since their token was issued.
/// If it has, returns 401 to force re-authentication with a fresh token carrying the updated role.
/// </summary>
public class RoleValidationMiddleware
{
    private readonly RequestDelegate _next;

    public RoleValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, PublishingTrackerDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            var iatClaim = context.User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat);

            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId) && iatClaim != null)
            {
                if (long.TryParse(iatClaim.Value, out long iatEpoch))
                {
                    var tokenIssuedAt = DateTimeOffset.FromUnixTimeSeconds(iatEpoch).UtcDateTime;

                    // Only query the database if the token has an iat we can compare.
                    // Use IgnoreQueryFilters since User has no global filter, and we want to find ANY user by ID.
                    var user = await dbContext.Users
                        .AsNoTracking()
                        .Where(u => u.Id == userId)
                        .Select(u => new { u.RoleChangedAt })
                        .FirstOrDefaultAsync();

                    if (user?.RoleChangedAt != null && user.RoleChangedAt > tokenIssuedAt)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new { error = "Your role has been updated. Please log in again." });
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}

public static class RoleValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseRoleValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RoleValidationMiddleware>();
    }
}
