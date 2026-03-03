using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Api.Models.Dtos;

namespace PublishingTracker.Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var dashboardGroup = app.MapGroup("/api/dashboard").RequireAuthorization();

        dashboardGroup.MapGet("/all", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext) =>
        {
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            // 1. Summary with Currency Grouping
            var revenueByCurrency = await db.Sales
                .ForUser(userId)
                .GroupBy(s => s.Currency)
                .Select(g => new CurrencyTotalDto { Currency = g.Key, TotalAmount = g.Sum(s => s.Revenue) })
                .ToListAsync();

            var totalBooksPublished = await db.Books.ForUser(userId).CountAsync();
            var totalSalesTransactions = await db.Sales.ForUser(userId).CountAsync();

            var topBook = await db.Sales
                .ForUser(userId)
                .GroupBy(s => s.Book.Title)
                .Select(g => new { Title = g.Key, Revenue = g.Sum(s => s.Revenue) })
                .OrderByDescending(x => x.Revenue)
                .FirstOrDefaultAsync();

            var topPlatform = await db.Sales
                .ForUser(userId)
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

            // 2. YOY
            var currentYear = DateTime.UtcNow.Year;
            var currentYearRevenue = await db.Sales.ForUser(userId).InYear(currentYear).SumAsync(s => s.Revenue);
            var lastYearRevenue = await db.Sales.ForUser(userId).InYear(currentYear - 1).SumAsync(s => s.Revenue);

            var yoy = new
            {
                currentYearRevenue,
                lastYearRevenue,
                growth = lastYearRevenue > 0 ? (double)(currentYearRevenue - lastYearRevenue) / (double)lastYearRevenue : 0
            };

            // 3. Seasonal
            var seasonal = await db.Sales
                .ForUser(userId)
                .InYear(currentYear)
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

        // Maintain legacy endpoints for compatibility
        dashboardGroup.MapGet("/summary", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext) =>
        {
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            var revenueByCurrency = await db.Sales
                .ForUser(userId)
                .GroupBy(s => s.Currency)
                .Select(g => new CurrencyTotalDto { Currency = g.Key, TotalAmount = g.Sum(s => s.Revenue) })
                .ToListAsync();
            
            return Results.Ok(new DashboardSummaryDto { 
                RevenueByCurrency = revenueByCurrency,
                TotalBooksPublished = await db.Books.ForUser(userId).CountAsync(),
                TotalSalesTransactions = await db.Sales.ForUser(userId).CountAsync()
            });
        });
    }
}