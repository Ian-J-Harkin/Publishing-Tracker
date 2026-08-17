using Microsoft.AspNetCore.Mvc;
using PublishingTracker.Api.Models;
using PublishingTracker.Domain.Dtos;
using MediatR;
using PublishingTracker.Application.Books.Commands;
using PublishingTracker.Application.Books.Queries; 
namespace PublishingTracker.Api.Features.Books;

public static class BookEndpoints
{
    public static void MapBookEndpoints(this WebApplication app)
    {
        var booksGroup = app.MapGroup("/api/books").RequireAuthorization();

        booksGroup.MapPost("/", async ([FromServices] IMediator mediator, CreateBookDto createBookDto) =>
        {
            // 1. Create the command from the incoming DTO
            var command = new CreateBookCommand(createBookDto.Title, createBookDto.Author, createBookDto.ISBN, createBookDto.PublicationDate, createBookDto.BasePrice, createBookDto.Genre, createBookDto.Description);

            // 2. Send the command through MediatR. 
            // MediatR will automatically find your CreateBookCommandHandler and execute it!
            var result = await mediator.Send(command);

            if (!result.IsSuccess)
                return result.ErrorMessage == "Unauthorized" ? Results.Unauthorized() : Results.BadRequest(result.ErrorMessage);

            return Results.Created($"/api/books/{result.Value}", result.Value);
        });

        booksGroup.MapGet("/", async ([FromServices] IMediator mediator, [FromQuery] string? search) =>
        {
            // TODO: adjust pagination parameters as needed
            var query = new GetBooksQuery(search, null, null, 1, 10);
            var result = await mediator.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.ErrorMessage);
        });

        booksGroup.MapGet("/{id}", async ([FromServices] IMediator mediator, int id) =>
        {
            // TODO: implement GetBookByIdQuery and its handler
            var query = new GetBookByIdQuery(id);
            var result = await mediator.Send(query);
            if (!result.IsSuccess)
            {
                if (result.ErrorMessage != null && result.ErrorMessage.Contains("Access denied"))
                    return Results.Forbid();
                return Results.NotFound(result.ErrorMessage);
            }
            return Results.Ok(result.Value);
        });

        // Deprecated endpoint – creation now handled by the MediatR command at the top of the file (line 15‑28).

        booksGroup.MapPut("/{id}", async ([FromServices] IMediator mediator, int id, UpdateBookDto updateBookDto) =>
        {
            var command = new UpdateBookCommand(id, updateBookDto.Title ?? string.Empty, updateBookDto.Author ?? string.Empty, updateBookDto.ISBN, updateBookDto.PublicationDate, updateBookDto.BasePrice, updateBookDto.Genre, updateBookDto.Description);
            var result = await mediator.Send(command);
            if (!result.IsSuccess)
            {
                if (result.ErrorMessage != null && result.ErrorMessage.Contains("Access denied"))
                    return Results.Forbid();
                return Results.NotFound(result.ErrorMessage);
            }
            return Results.Ok(result.Value);
        });

        booksGroup.MapDelete("/{id}", async ([FromServices] IMediator mediator, int id) =>
        {
            var command = new DeleteBookCommand(id);
            var result = await mediator.Send(command);
            if (!result.IsSuccess)
            {
                if (result.ErrorMessage != null && result.ErrorMessage.Contains("Access denied"))
                    return Results.Forbid();
                return Results.NotFound(result.ErrorMessage);
            }
            return Results.NoContent();
        });

        booksGroup.MapGet("/{id}/performance", async ([FromServices] IMediator mediator, int id) =>
        {
            var query = new GetBookPerformanceQuery(id);
            var result = await mediator.Send(query);
            if (!result.IsSuccess)
            {
                if (result.ErrorMessage != null && result.ErrorMessage.Contains("Access denied"))
                    return Results.Forbid();
                return Results.NotFound(result.ErrorMessage);
            }
            return Results.Ok(result.Value);
        });
    }
}
