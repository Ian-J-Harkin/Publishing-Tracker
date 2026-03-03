using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;

namespace PublishingTracker.Api.Features.Sales;

public static class SalesEndpoints
{
    public static void MapSalesEndpoints(this WebApplication app)
    {
        var salesGroup = app.MapGroup("/api/sales").RequireAuthorization();

        salesGroup.MapGet("/", async (
            [FromServices] PublishingTrackerDbContext db, 
            HttpContext httpContext, 
            [FromQuery] int? bookId,
            [FromQuery] int? platformId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("SalesEndpoints");
            try
            {
                if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                    return errorResult!;

                var sales = await db.Sales
                    .Include(s => s.Book)
                    .Include(s => s.Platform)
                    .ForUser(userId)
                    .FilterByBook(bookId)
                    .FilterByPlatform(platformId)
                    .InDateRange(startDate, endDate)
                    .OrderByDescending(s => s.SaleDate)
                    .Select(s => s.ToDto())
                    .ToListAsync();

                return Results.Ok(sales);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching filtered sales.");
                return Results.Problem("Failed to fetch sales.");
            }
        });

        salesGroup.MapGet("/summary", async (
            [FromServices] PublishingTrackerDbContext db, 
            HttpContext httpContext,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate) =>
        {
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            var summary = await db.Sales
                .ForUser(userId)
                .InDateRange(startDate, endDate)
                .GroupBy(s => 1)
                .Select(g => new SalesSummaryDto
                {
                    TotalRevenue = g.Sum(s => s.Revenue),
                    TotalUnitsSold = g.Sum(s => s.Quantity),
                    AverageRoyalty = g.Count() > 0 ? g.Sum(s => s.Royalty) / g.Count() : 0,
                    SalesCount = g.Count()
                }).FirstOrDefaultAsync() ?? new SalesSummaryDto();

            return Results.Ok(summary);
        });

        salesGroup.MapPost("/", async ([FromServices] PublishingTrackerDbContext db, CreateSaleDto createSaleDto, HttpContext httpContext, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("SalesEndpoints");
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            var book = await db.Books.FirstOrDefaultAsync(b => b.Id == createSaleDto.BookId && b.UserId == userId);
            if (book == null)
            {
                logger.LogWarning("User {UserId} tried to create a sale for a book they do not own (BookId: {BookId})", userId, createSaleDto.BookId);
                return Results.NotFound("Book not found or does not belong to the user.");
            }

            var platform = await db.Platforms.FirstOrDefaultAsync(p => p.Id == createSaleDto.PlatformId);
            if (platform == null)
            {
                logger.LogWarning("Platform with ID {PlatformId} not found", createSaleDto.PlatformId);
                return Results.NotFound("Platform not found.");
            }

            var sale = new Sale
            {
                BookId = createSaleDto.BookId,
                PlatformId = createSaleDto.PlatformId,
                SaleDate = createSaleDto.SaleDate,
                Quantity = createSaleDto.Quantity,
                UnitPrice = createSaleDto.UnitPrice,
                Royalty = createSaleDto.Royalty,
                Revenue = createSaleDto.Quantity * createSaleDto.Royalty,
                Currency = createSaleDto.Currency,
                CreatedAt = DateTime.UtcNow
            };

            db.Sales.Add(sale);
            await db.SaveChangesAsync();

            logger.LogInformation("User {UserId} created a sale (SaleId: {SaleId}) for BookId {BookId}", userId, sale.Id, sale.BookId);

            return Results.Created($"/api/sales/{sale.Id}", sale.ToDto(book.Title, platform.Name));
        });
    }
}