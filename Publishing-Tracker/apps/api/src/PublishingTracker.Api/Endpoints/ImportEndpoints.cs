using MediatR;
using Microsoft.AspNetCore.Mvc;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Application.Import.Commands;
using PublishingTracker.Application.Import.Queries;

namespace PublishingTracker.Api.Endpoints;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this WebApplication app)
    {
        var importGroup = app.MapGroup("/api/import").RequireAuthorization().DisableAntiforgery();

        importGroup.MapPost("/upload", async (
            [FromServices] IMediator mediator,
            IFormFile file,
            HttpContext httpContext) =>
        {
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            await using var stream = file.OpenReadStream();
            var result = await mediator.Send(new UploadImportFileCommand(userId, Path.GetFileName(file.FileName), stream));
            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.BadRequest(result.ErrorMessage ?? "Upload failed");
        });

        importGroup.MapPost("/process", async (
            [FromServices] IMediator mediator,
            ProcessImportRequest request,
            HttpContext httpContext) =>
        {
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "temp_uploads", $"{userId}_{request.FileName}");
            var result = await mediator.Send(new CreateImportJobCommand(userId, request.FileName, filePath, request.Mapping));
            return result.IsSuccess
                ? Results.Accepted($"/api/import/status/{result.Value?.Id}", result.Value)
                : ApiResults.BadRequest(result.ErrorMessage ?? "Processing failed");
        });

        importGroup.MapGet("/status/{jobId}", async (
            [FromServices] IMediator mediator,
            HttpContext httpContext,
            int jobId) =>
        {
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            var result = await mediator.Send(new GetImportJobStatusQuery(userId, jobId));
            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.NotFound(result.ErrorMessage ?? "Not found");
        });

        importGroup.MapGet("/history", async (
            [FromServices] IMediator mediator,
            HttpContext httpContext,
            [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("ImportEndpoints");
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            logger.LogInformation("Fetching import history for User {UserId}.", userId);
            var result = await mediator.Send(new GetImportJobHistoryQuery(userId));
            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.BadRequest(result.ErrorMessage ?? "Request failed");
        });
    }

    public record ProcessImportRequest(string FileName, ColumnMappingDto Mapping);
}
