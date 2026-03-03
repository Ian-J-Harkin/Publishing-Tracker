using System.Security.Claims;

namespace PublishingTracker.Api.Extensions;

/// <summary>
/// Extension methods for <see cref="HttpContext"/> to simplify user identity extraction.
/// Replaces the duplicated TryGetUserId helper found in SalesEndpoints, DashboardEndpoints,
/// ImportEndpoints, and PlatformEndpoints.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Extracts the authenticated user's ID from the JWT claims.
    /// Returns null if the user is not authenticated or the claim is missing/invalid.
    /// </summary>
    public static int? GetUserId(this HttpContext context)
    {
        var claim = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    /// <summary>
    /// Extracts the authenticated user's ID, returning an <see cref="IResult"/> error
    /// if the user is not authenticated. Intended for use in Minimal API endpoint handlers.
    /// </summary>
    public static bool TryGetUserId(this HttpContext context, out int userId, out IResult? errorResult)
    {
        var id = context.GetUserId();
        if (id.HasValue)
        {
            userId = id.Value;
            errorResult = null;
            return true;
        }

        userId = 0;
        errorResult = Results.Unauthorized();
        return false;
    }
}
