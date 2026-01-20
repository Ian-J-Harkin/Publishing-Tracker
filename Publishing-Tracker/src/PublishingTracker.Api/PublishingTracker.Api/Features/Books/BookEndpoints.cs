using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;
using System.Security.Claims;

namespace PublishingTracker.Api.Features.Books;

public static class BookEndpoints
{
    public static void MapBookEndpoints(this WebApplication app)
    {
        var booksGroup = app.MapGroup("/api/books").RequireAuthorization();

        booksGroup.MapGet("/", async ([FromServices] PublishingTrackerDbContext db, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BookEndpoints");
            logger.LogInformation("Fetching all books.");
            var books = await db.Books.ToListAsync();
            return Results.Ok(books);
        });

        booksGroup.MapGet("/{id}", async ([FromServices] PublishingTrackerDbContext db, int id, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BookEndpoints");
            logger.LogInformation("Fetching book with ID {BookId}.", id);
            var book = await db.Books.FindAsync(id);
            if (book == null)
            {
                logger.LogWarning("Book with ID {BookId} not found.", id);
            }
            return book == null ? Results.NotFound() : Results.Ok(book);
        });

        booksGroup.MapPost("/", async ([FromServices] PublishingTrackerDbContext db, CreateBookDto createBookDto, HttpContext httpContext, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BookEndpoints");
            if (!TryGetUserId(httpContext, out var userId))
            {
                logger.LogWarning("Could not retrieve user ID from token during book creation.");
                return Results.Unauthorized();
            }

            var book = new Book
            {
                Title = createBookDto.Title,
                Author = createBookDto.Author,
                ISBN = createBookDto.ISBN,
                PublicationDate = createBookDto.PublicationDate,
                BasePrice = createBookDto.BasePrice,
                Genre = createBookDto.Genre,
                Description = createBookDto.Description,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Books.Add(book);
            await db.SaveChangesAsync();

            logger.LogInformation("User {UserId} created a new book (BookId: {BookId}).", userId, book.Id);
            return Results.Created($"/api/books/{book.Id}", book);
        });

        booksGroup.MapPut("/{id}", async ([FromServices] PublishingTrackerDbContext db, int id, UpdateBookDto updateBookDto, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BookEndpoints");
            var book = await db.Books.FindAsync(id);
            if (book == null)
            {
                logger.LogWarning("Book with ID {BookId} not found for update.", id);
                return Results.NotFound();
            }

            if (updateBookDto.Title != null) book.Title = updateBookDto.Title;
            if (updateBookDto.Author != null) book.Author = updateBookDto.Author;
            if (updateBookDto.ISBN != null) book.ISBN = updateBookDto.ISBN;
            if (updateBookDto.PublicationDate.HasValue) book.PublicationDate = updateBookDto.PublicationDate;
            if (updateBookDto.BasePrice.HasValue) book.BasePrice = updateBookDto.BasePrice;
            if (updateBookDto.Genre != null) book.Genre = updateBookDto.Genre;
            if (updateBookDto.Description != null) book.Description = updateBookDto.Description;
            book.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            logger.LogInformation("Book with ID {BookId} was updated.", id);
            return Results.Ok(book);
        });

        booksGroup.MapDelete("/{id}", async ([FromServices] PublishingTrackerDbContext db, int id, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BookEndpoints");
            var book = await db.Books.FindAsync(id);
            if (book == null)
            {
                logger.LogWarning("Book with ID {BookId} not found for deletion.", id);
                return Results.NotFound();
            }

            db.Books.Remove(book);
            await db.SaveChangesAsync();

            logger.LogInformation("Book with ID {BookId} was deleted.", id);
            return Results.NoContent();
        });

        booksGroup.MapGet("/{id}/performance", async ([FromServices] PublishingTrackerDbContext db, int id, HttpContext httpContext, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BookEndpoints");
            if (!TryGetUserId(httpContext, out var userId))
            {
                logger.LogWarning("Could not retrieve user ID from token when fetching book performance.");
                return Results.Unauthorized();
            }

            var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
            if (book == null)
            {
                logger.LogWarning("User {UserId} tried to access performance data for a book they do not own (BookId: {BookId}).", userId, id);
                return Results.NotFound("Book not found or does not belong to the user.");
            }

            logger.LogInformation("Fetching performance data for BookId {BookId} for User {UserId}.", id, userId);
            var performanceData = await db.Sales
                .Where(s => s.BookId == id)
                .GroupBy(s => s.Platform.Name)
                .Select(g => new { PlatformName = g.Key, TotalRevenue = g.Sum(s => s.Revenue) })
                .ToListAsync();

            return Results.Ok(performanceData);
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