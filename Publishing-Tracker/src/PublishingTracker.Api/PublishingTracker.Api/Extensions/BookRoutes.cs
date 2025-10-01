using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;

namespace PublishingTracker.Api.Extensions;

public static class BookRoutes
{
    public static void MapBookRoutes(this WebApplication app)
    {
        var booksGroup = app.MapGroup("/api/books").RequireAuthorization();

        booksGroup.MapGet("/", async ([FromServices] PublishingTrackerDbContext db) =>
        {
            var books = await db.Books.ToListAsync();
            return Results.Ok(books);
        });

        booksGroup.MapGet("/{id}", async ([FromServices] PublishingTrackerDbContext db, int id) =>
        {
            var book = await db.Books.FindAsync(id);
            return book == null ? Results.NotFound() : Results.Ok(book);
        });

        booksGroup.MapPost("/", async ([FromServices] PublishingTrackerDbContext db, CreateBookDto createBookDto, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);

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

            return Results.Created($"/api/books/{book.Id}", book);
        });

        booksGroup.MapPut("/{id}", async ([FromServices] PublishingTrackerDbContext db, int id, UpdateBookDto updateBookDto) =>
        {
            var book = await db.Books.FindAsync(id);
            if (book == null)
            {
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
            return Results.Ok(book);
        });

        booksGroup.MapDelete("/{id}", async ([FromServices] PublishingTrackerDbContext db, int id) =>
        {
            var book = await db.Books.FindAsync(id);
            if (book == null)
            {
                return Results.NotFound();
            }

            db.Books.Remove(book);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        booksGroup.MapGet("/{id}/performance", async ([FromServices] PublishingTrackerDbContext db, int id, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);

            var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
            if (book == null)
            {
                return Results.NotFound("Book not found or does not belong to the user.");
            }

            var performanceData = await db.Sales
                .Where(s => s.BookId == id)
                .GroupBy(s => s.Platform.Name)
                .Select(g => new { PlatformName = g.Key, TotalRevenue = g.Sum(s => s.Revenue) })
                .ToListAsync();

            return Results.Ok(performanceData);
        });
    }
}