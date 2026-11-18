using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Common.Extensions;
using PublishingTracker.Domain;
using Microsoft.AspNetCore.Mvc;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Application.Common.Dtos;
using MediatR;
using PublishingTracker.Application.Books.Commands;
using PublishingTracker.Application.Books.Queries; 
namespace PublishingTracker.Api.Endpoints;

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
                return result.ErrorMessage == "Unauthorized"
                    ? ApiResults.Unauthorized("Unauthorized")
                    : ApiResults.BadRequest(result.ErrorMessage ?? "Request failed");

            return Results.Created($"/api/books/{result.Value}", result.Value);
        });

        booksGroup.MapGet("/", async ([FromServices] IMediator mediator, [FromQuery] string? search) =>
        {
            // TODO: adjust pagination parameters as needed
            var query = new GetBooksQuery(search, null, null, 1, 10);
            var result = await mediator.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.BadRequest(result.ErrorMessage ?? "Request failed");
        });

        booksGroup.MapGet("/missing-metadata", async (
            [FromServices] IMediator mediator,
            [FromQuery] string? format,
            [FromQuery] string? missingField,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default) =>
        {
            var result = await mediator.Send(new FindTitlesMissingMetadataQuery(format, missingField, page, pageSize), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : MapCatalogueError(result.ErrorMessage);
        });

        booksGroup.MapGet("/{id:int}/details", async (
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetTitleDetailsQuery(id), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : MapCatalogueError(result.ErrorMessage);
        });

        booksGroup.MapGet("/{id}", async ([FromServices] IMediator mediator, int id) =>
        {
            // TODO: implement GetBookByIdQuery and its handler
            var query = new GetBookByIdQuery(id);
            var result = await mediator.Send(query);
            if (!result.IsSuccess)
            {
                if (result.ErrorMessage != null && result.ErrorMessage.Contains("Access denied"))
                    return ApiResults.Forbidden("Access denied");
                return ApiResults.NotFound(result.ErrorMessage ?? "Not found");
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
                    return ApiResults.Forbidden("Access denied");
                return ApiResults.NotFound(result.ErrorMessage ?? "Not found");
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
                    return ApiResults.Forbidden("Access denied");
                return ApiResults.NotFound(result.ErrorMessage ?? "Not found");
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

    private static IResult MapCatalogueError(string? message)
    {
        var text = message ?? "Request failed";
        if (text.StartsWith("UNAUTHORIZED:", StringComparison.Ordinal)) return ApiResults.Unauthorized(text);
        if (text.StartsWith("NOT_FOUND_OR_UNAUTHORIZED:", StringComparison.Ordinal)) return ApiResults.NotFound(text);
        return ApiResults.BadRequest(text);
    }
}
