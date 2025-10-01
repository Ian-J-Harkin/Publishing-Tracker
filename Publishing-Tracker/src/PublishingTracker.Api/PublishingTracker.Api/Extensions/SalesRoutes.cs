using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;

namespace PublishingTracker.Api.Extensions;

public static class SalesRoutes
{
    public static void MapSalesRoutes(this WebApplication app)
    {
        var salesGroup = app.MapGroup("/api/sales").RequireAuthorization();

        salesGroup.MapGet("/", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
            var sales = await db.Sales
                .Where(s => s.Book.UserId == userId)
                .Select(s => new SaleDto
                {
                    Id = s.Id,
                    BookId = s.BookId,
                    BookTitle = s.Book.Title,
                    PlatformId = s.PlatformId,
                    PlatformName = s.Platform.Name,
                    SaleDate = s.SaleDate,
                    Quantity = s.Quantity,
                    UnitPrice = s.UnitPrice,
                    Royalty = s.Royalty,
                    Revenue = s.Revenue
                })
                .ToListAsync();
            return Results.Ok(sales);
        });

        salesGroup.MapPost("/", async ([FromServices] PublishingTrackerDbContext db, CreateSaleDto createSaleDto, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
            var book = await db.Books.FirstOrDefaultAsync(b => b.Id == createSaleDto.BookId && b.UserId == userId);
            if (book == null)
            {
                return Results.NotFound("Book not found or does not belong to the user.");
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
                CreatedAt = DateTime.UtcNow
            };

            db.Sales.Add(sale);
            await db.SaveChangesAsync();

            return Results.Created($"/api/sales/{sale.Id}", sale);
        });
    }
}