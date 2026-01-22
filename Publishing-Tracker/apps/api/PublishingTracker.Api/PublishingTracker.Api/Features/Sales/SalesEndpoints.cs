using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;
using System;
using System.Security.Claims;

namespace PublishingTracker.Api.Features.Sales;

public static class SalesEndpoints
{
    public static void MapSalesEndpoints(this WebApplication app)
    {
        ///         csharp PublishingTracker.Api\Features\Sales\SalesEndpoints.cs
        // Replace the existing MapGet("/") handler body with this temporary, more-verbose version.
        var salesGroup = app.MapGroup("/api/sales").RequireAuthorization();
        salesGroup.MapGet("/", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("SalesEndpoints");
            try
            {
                if (!TryGetUserId(httpContext, out var userId))
                {
                    logger.LogWarning("Could not retrieve user ID from token.");
                    return Results.Unauthorized();
                }

                logger.LogInformation("Fetching sales for User {UserId}", userId);
                var sales = await db.Sales
                    .Include(s => s.Book)   
                    .Include(s => s.Platform)
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
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while fetching sales for request. UserClaims: {Claims}",
                    string.Join(", ", httpContext.User.Claims.Select(c => $"{c.Type}={c.Value}")));
                // return a safe Problem response (do not expose internal exception to client in prod)
                return Results.Problem(detail: "Failed to fetch sales. Check server logs for details.", statusCode: 500);
            }
        });
        ///
        //var salesGroup = app.MapGroup("/api/sales").RequireAuthorization();

        //salesGroup.MapGet("/", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext, [FromServices] ILoggerFactory loggerFactory) =>
        //{
        //    var logger = loggerFactory.CreateLogger("SalesEndpoints");
        //    if (!TryGetUserId(httpContext, out var userId))
        //    {
        //        logger.LogWarning("Could not retrieve user ID from token.");
        //        return Results.Unauthorized();
        //    }

        //    logger.LogInformation("Fetching sales for User {UserId}", userId);
        //    var sales = await db.Sales
        //        .Where(s => s.Book.UserId == userId)
        //        .Select(s => new SaleDto
        //        {
        //            Id = s.Id,
        //            BookId = s.BookId,
        //            BookTitle = s.Book.Title,
        //            PlatformId = s.PlatformId,
        //            PlatformName = s.Platform.Name,
        //            SaleDate = s.SaleDate,
        //            Quantity = s.Quantity,
        //            UnitPrice = s.UnitPrice,
        //            Royalty = s.Royalty,
        //            Revenue = s.Revenue
        //        })
        //        .ToListAsync();
        //    return Results.Ok(sales);
        //});

        salesGroup.MapPost("/", async ([FromServices] PublishingTrackerDbContext db, CreateSaleDto createSaleDto, HttpContext httpContext, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("SalesEndpoints");
            if (!TryGetUserId(httpContext, out var userId))
            {
                logger.LogWarning("Could not retrieve user ID from token during sale creation.");
                return Results.Unauthorized();
            }

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
                CreatedAt = DateTime.UtcNow
            };

            db.Sales.Add(sale);
            await db.SaveChangesAsync();

            logger.LogInformation("User {UserId} created a sale (SaleId: {SaleId}) for BookId {BookId}", userId, sale.Id, sale.BookId);

            // Create a SaleDto to return instead of the raw Sale entity
            var saleDto = new SaleDto
            {
                Id = sale.Id,
                BookId = sale.BookId,
                BookTitle = book.Title,
                PlatformId = sale.PlatformId,
                PlatformName = platform.Name,
                SaleDate = sale.SaleDate,
                Quantity = sale.Quantity,
                UnitPrice = sale.UnitPrice,
                Royalty = sale.Royalty,
                Revenue = sale.Revenue
            };

            return Results.Created($"/api/sales/{sale.Id}", saleDto);
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