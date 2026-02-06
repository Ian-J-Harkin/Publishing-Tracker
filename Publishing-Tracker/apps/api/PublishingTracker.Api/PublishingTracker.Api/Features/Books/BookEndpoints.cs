using Microsoft.AspNetCore.Mvc;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;
using PublishingTracker.Api.Services;

namespace PublishingTracker.Api.Features.Books;

public static class BookEndpoints
{
    public static void MapBookEndpoints(this WebApplication app)
    {
        var booksGroup = app.MapGroup("/api/books").RequireAuthorization();

        booksGroup.MapGet("/", async ([FromServices] IBookService bookService) =>
        {
            var books = await bookService.GetAllAsync();
            return Results.Ok(books);
        });

        booksGroup.MapGet("/{id}", async ([FromServices] IBookService bookService, int id) =>
        {
            var book = await bookService.GetByIdAsync(id);
            return book == null ? Results.NotFound() : Results.Ok(book);
        });

        booksGroup.MapPost("/", async ([FromServices] IBookService bookService, CreateBookDto createBookDto) =>
        {
            try
            {
                var book = await bookService.CreateAsync(createBookDto);
                return Results.Created($"/api/books/{book.Id}", book);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });

        booksGroup.MapPut("/{id}", async ([FromServices] IBookService bookService, int id, UpdateBookDto updateBookDto) =>
        {
            var book = await bookService.UpdateAsync(id, updateBookDto);
            return book == null ? Results.NotFound() : Results.Ok(book);
        });

        booksGroup.MapDelete("/{id}", async ([FromServices] IBookService bookService, int id) =>
        {
            await bookService.DeleteAsync(id);
            return Results.NoContent();
        });

        booksGroup.MapGet("/{id}/performance", async ([FromServices] IBookService bookService, int id) =>
        {
            var performanceData = await bookService.GetPerformanceAsync(id);
            
            if (performanceData == null)
            {
                // This could mean book not found OR user unauthorized for book. 
                // To keep it simple and secure, we can return NotFound or generic 'Access Denied'.
                // Since service handles logic, null implies "cannot retrieve data".
                return Results.NotFound("Book not found or access denied.");
            }

            return Results.Ok(performanceData);
        });
    }
}
