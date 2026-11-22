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

            if (!TrySanitizeImportFileName(file.FileName, out var safeFileName))
                return ApiResults.BadRequest("Invalid file name.");

            await using var stream = file.OpenReadStream();
            var result = await mediator.Send(new UploadImportFileCommand(userId, safeFileName, stream));
            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.BadRequest(result.ErrorMessage ?? "Upload failed");
        });

        importGroup.MapPost("/process", async (
            [FromServices] IMediator mediator,
            ProcessImportRequest request,
            HttpContext httpContext) =>
        {
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            if (!TrySanitizeImportFileName(request.FileName, out var safeFileName))
                return ApiResults.BadRequest("Invalid file name.");

            var tempDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "temp_uploads"));
            var filePath = Path.GetFullPath(Path.Combine(tempDir, $"{userId}_{safeFileName}"));
            if (!filePath.StartsWith(tempDir + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                !string.Equals(Path.GetDirectoryName(filePath), tempDir, StringComparison.Ordinal))
            {
                return ApiResults.BadRequest("Invalid file path.");
            }

            var result = await mediator.Send(new CreateImportJobCommand(userId, safeFileName, filePath, request.Mapping));
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

    private static bool TrySanitizeImportFileName(string? fileName, out string safeFileName)
    {
        safeFileName = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var trimmed = fileName.Trim();
        var extracted = Path.GetFileName(trimmed);
        if (!string.Equals(trimmed, extracted, StringComparison.Ordinal) ||
            trimmed.Contains("..", StringComparison.Ordinal) ||
            trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            trimmed is "." or "..")
        {
            return false;
        }

        safeFileName = extracted;
        return true;
    }

    public record ProcessImportRequest(string FileName, ColumnMappingDto Mapping);
}
