using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models.Dtos;

namespace PublishingTracker.Api.Extensions;

public static class DashboardRoutes
{
    public static void MapDashboardRoutes(this WebApplication app)
    {
        var dashboardGroup = app.MapGroup("/api/dashboard").RequireAuthorization();

        dashboardGroup.MapGet("/summary", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);

            var totalRevenue = await db.Sales
                .Where(s => s.Book.UserId == userId)
                .SumAsync(s => s.Revenue);

            var totalBooksPublished = await db.Books
                .CountAsync(b => b.UserId == userId);

            var totalSalesTransactions = await db.Sales
                .CountAsync(s => s.Book.UserId == userId);

            var topPerformingBook = await db.Sales
                .Where(s => s.Book.UserId == userId)
                .GroupBy(s => s.Book.Title)
                .Select(g => new { Title = g.Key, TotalRevenue = g.Sum(s => s.Revenue) })
                .OrderByDescending(x => x.TotalRevenue)
                .FirstOrDefaultAsync();

            var topPerformingPlatform = await db.Sales
                .Where(s => s.Book.UserId == userId)
                .GroupBy(s => s.Platform.Name)
                .Select(g => new { PlatformName = g.Key, TotalRevenue = g.Sum(s => s.Revenue) })
                .OrderByDescending(x => x.TotalRevenue)
                .FirstOrDefaultAsync();

            var summary = new DashboardSummaryDto
            {
                TotalRevenue = totalRevenue,
                TotalBooksPublished = totalBooksPublished,
                TotalSalesTransactions = totalSalesTransactions,
                TopPerformingBook = topPerformingBook?.Title ?? "N/A",
                TopPerformingPlatform = topPerformingPlatform?.PlatformName ?? "N/A"
            };

            return Results.Ok(summary);
        });

        dashboardGroup.MapGet("/yoy", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);

            var currentYearRevenue = await db.Sales
                .Where(s => s.Book.UserId == userId && s.SaleDate.Year == DateTime.Now.Year)
                .SumAsync(s => s.Revenue);

            var previousYearRevenue = await db.Sales
                .Where(s => s.Book.UserId == userId && s.SaleDate.Year == DateTime.Now.Year - 1)
                .SumAsync(s => s.Revenue);

            var yoyComparison = new
            {
                CurrentYearRevenue = currentYearRevenue,
                PreviousYearRevenue = previousYearRevenue,
                Growth = previousYearRevenue > 0 ? (currentYearRevenue - previousYearRevenue) / previousYearRevenue : 0
            };

            return Results.Ok(yoyComparison);
        });

        dashboardGroup.MapGet("/seasonal", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);

            var seasonalData = await db.Sales
                .Where(s => s.Book.UserId == userId)
                .GroupBy(s => s.SaleDate.Month)
                .Select(g => new { Month = g.Key, TotalRevenue = g.Sum(s => s.Revenue) })
                .OrderBy(x => x.Month)
                .ToListAsync();

            return Results.Ok(seasonalData);
        });
    }
}