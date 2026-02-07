using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models.Dtos;
using System.Security.Claims;

namespace PublishingTracker.Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var dashboardGroup = app.MapGroup("/api/dashboard").RequireAuthorization();

        dashboardGroup.MapGet("/all", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext) =>
        {
            if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();

            // 1. Summary with Currency Grouping
            var revenueByCurrency = await db.Sales
                .Where(s => s.Book.UserId == userId)
                .GroupBy(s => s.Currency)
                .Select(g => new CurrencyTotalDto { Currency = g.Key, TotalAmount = g.Sum(s => s.Revenue) })
                .ToListAsync();

            var totalBooksPublished = await db.Books.CountAsync(b => b.UserId == userId);
            var totalSalesTransactions = await db.Sales.CountAsync(s => s.Book.UserId == userId);

            var topBook = await db.Sales
                .Where(s => s.Book.UserId == userId)
                .GroupBy(s => s.Book.Title)
                .Select(g => new { Title = g.Key, Revenue = g.Sum(s => s.Revenue) })
                .OrderByDescending(x => x.Revenue)
                .FirstOrDefaultAsync();

            var topPlatform = await db.Sales
                .Where(s => s.Book.UserId == userId)
                .GroupBy(s => s.Platform.Name)
                .Select(g => new { Name = g.Key, Revenue = g.Sum(s => s.Revenue) })
                .OrderByDescending(x => x.Revenue)
                .FirstOrDefaultAsync();

            var summary = new DashboardSummaryDto
            {
                RevenueByCurrency = revenueByCurrency,
                TotalBooksPublished = totalBooksPublished,
                TotalSalesTransactions = totalSalesTransactions,
                TopPerformingBook = topBook?.Title ?? "N/A",
                TopPerformingPlatform = topPlatform?.Name ?? "N/A"
            };

            // 2. YOY (Filtering to current year by default for simplicity)
            var currentYear = DateTime.UtcNow.Year;
            var currentYearRevenue = await db.Sales
                .Where(s => s.Book.UserId == userId && s.SaleDate.Year == currentYear)
                .SumAsync(s => s.Revenue);
            var lastYearRevenue = await db.Sales
                .Where(s => s.Book.UserId == userId && s.SaleDate.Year == currentYear - 1)
                .SumAsync(s => s.Revenue);

            var yoy = new
            {
                currentYearRevenue,
                lastYearRevenue,
                growth = lastYearRevenue > 0 ? (double)(currentYearRevenue - lastYearRevenue) / (double)lastYearRevenue : 0
            };

            // 3. Seasonal
            var seasonal = await db.Sales
                .Where(s => s.Book.UserId == userId && s.SaleDate.Year == currentYear)
                .GroupBy(s => s.SaleDate.Month)
                .Select(g => new { Month = g.Key, TotalRevenue = g.Sum(s => s.Revenue) })
                .OrderBy(x => x.Month)
                .ToListAsync();

            return Results.Ok(new DashboardDataDto
            {
                Summary = summary,
                YoY = yoy,
                Seasonal = seasonal
            });
        });

        // Maintain legacy endpoints for compatibility but point to shared logic if needed
        dashboardGroup.MapGet("/summary", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext) =>
        {
            if (!TryGetUserId(httpContext, out var userId)) return Results.Unauthorized();
            var revenueByCurrency = await db.Sales
                .Where(s => s.Book.UserId == userId)
                .GroupBy(s => s.Currency)
                .Select(g => new CurrencyTotalDto { Currency = g.Key, TotalAmount = g.Sum(s => s.Revenue) })
                .ToListAsync();
            
            return Results.Ok(new DashboardSummaryDto { 
                RevenueByCurrency = revenueByCurrency,
                TotalBooksPublished = await db.Books.CountAsync(b => b.UserId == userId),
                TotalSalesTransactions = await db.Sales.CountAsync(s => s.Book.UserId == userId)
            });
        });
    }

    private static bool TryGetUserId(HttpContext httpContext, out int userId)
    {
        var userIdClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out userId))
        {
            return true;
        }

        userId = 0;
        return false;
    }
}