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

        booksGroup.MapGet("/", async ([FromServices] IBookService bookService, [FromQuery] string? search) =>
        {
            var result = await bookService.GetAllAsync(search);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.ErrorMessage);
        });

        booksGroup.MapGet("/{id}", async ([FromServices] IBookService bookService, int id) =>
        {
            var result = await bookService.GetByIdAsync(id);
            if (!result.IsSuccess)
            {
                if (result.ErrorMessage != null && result.ErrorMessage.Contains("Access denied"))
                    return Results.Forbid();
                return Results.NotFound(result.ErrorMessage);
            }
            return Results.Ok(result.Value);
        });

        booksGroup.MapPost("/", async ([FromServices] IBookService bookService, CreateBookDto createBookDto) =>
        {
            var result = await bookService.CreateAsync(createBookDto);
            if (!result.IsSuccess)
                return result.ErrorMessage == "User is not authenticated." ? Results.Unauthorized() : Results.BadRequest(result.ErrorMessage);

            return Results.Created($"/api/books/{result.Value!.Id}", result.Value);
        });

        booksGroup.MapPut("/{id}", async ([FromServices] IBookService bookService, int id, UpdateBookDto updateBookDto) =>
        {
            var result = await bookService.UpdateAsync(id, updateBookDto);
            if (!result.IsSuccess)
            {
                if (result.ErrorMessage != null && result.ErrorMessage.Contains("Access denied"))
                    return Results.Forbid();
                return Results.NotFound(result.ErrorMessage);
            }
            return Results.Ok(result.Value);
        });

        booksGroup.MapDelete("/{id}", async ([FromServices] IBookService bookService, int id) =>
        {
            var result = await bookService.DeleteAsync(id);
            if (!result.IsSuccess)
            {
                if (result.ErrorMessage != null && result.ErrorMessage.Contains("Access denied"))
                    return Results.Forbid();
                return Results.NotFound(result.ErrorMessage);
            }
            return Results.NoContent();
        });

        booksGroup.MapGet("/{id}/performance", async ([FromServices] IBookService bookService, int id) =>
        {
            var result = await bookService.GetPerformanceAsync(id);
            if (!result.IsSuccess)
            {
                if (result.ErrorMessage != null && result.ErrorMessage.Contains("access denied"))
                    return Results.Forbid();
                return Results.NotFound(result.ErrorMessage);
            }
            return Results.Ok(result.Value);
        });
    }
}
