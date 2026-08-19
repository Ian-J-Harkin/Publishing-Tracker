using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PublishingTracker.Api.Extensions;
using MediatR;
using PublishingTracker.Application.Dashboard.Queries;

namespace PublishingTracker.Application.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var dashboardGroup = app.MapGroup("/api/dashboard").RequireAuthorization();

        dashboardGroup.MapGet("/all", async ([FromServices] IMediator mediator, [FromServices] IMemoryCache cache, HttpContext httpContext) =>
        {
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            var cacheKey = $"dashboard_all_{userId}";

            var result = await cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await mediator.Send(new GetDashboardDataQuery(userId));
            });

            return Results.Ok(result);
        });

        // Maintain legacy endpoints for compatibility
        dashboardGroup.MapGet("/summary", async ([FromServices] IMediator mediator, HttpContext httpContext) =>
        {
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            var result = await mediator.Send(new GetDashboardSummaryQuery(userId));
            return Results.Ok(result);
        });
    }
}
