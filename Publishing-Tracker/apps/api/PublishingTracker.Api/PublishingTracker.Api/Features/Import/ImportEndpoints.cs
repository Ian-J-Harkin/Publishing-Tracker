using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models.Dtos;
using PublishingTracker.Api.Services;
using System.Security.Claims;

namespace PublishingTracker.Api.Features.Import;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this WebApplication app)
    {
        var importGroup = app.MapGroup("/api/import").RequireAuthorization().DisableAntiforgery();

        importGroup.MapPost("/upload", async ([FromServices] PublishingTrackerDbContext db, IFormFile file, HttpContext httpContext, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("ImportEndpoints");
            try
            {
                if (!TryGetUserId(httpContext, out var userId))
                {
                    return Results.Unauthorized();
                }

                logger.LogInformation("User {UserId} uploaded file {FileName}.", userId, file.FileName);
                
                // Ensure temp directory exists
                var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp_uploads");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                // Save file for processing step
                var filePath = Path.Combine(tempDir, $"{userId}_{file.FileName}");
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Extract headers and data preview
                var previewData = new PreviewDataDto { FileName = file.FileName };
                
                using (var reader = new StreamReader(filePath))
                using (var csv = new CsvHelper.CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture) { HasHeaderRecord = true }))
                {
                    await csv.ReadAsync();
                    csv.ReadHeader();
                    if (csv.HeaderRecord != null)
                    {
                        previewData.Headers = csv.HeaderRecord.ToList();
                    }

                    // Read first 5 rows for preview
                    int previewCount = 0;
                    while (await csv.ReadAsync() && previewCount < 5)
                    {
                        var row = new Dictionary<string, string>();
                        foreach (var header in previewData.Headers)
                        {
                            row[header] = csv.GetField(header) ?? string.Empty;
                        }
                        previewData.PreviewRows.Add(row);
                        previewCount++;
                    }
                }

                return Results.Ok(previewData);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during file upload");
                return Results.Problem(ex.Message);
            }
        });

        importGroup.MapPost("/process", async ([FromServices] PublishingTrackerDbContext db, [FromServices] ICsvImportService importService, ProcessImportRequest request, HttpContext httpContext) =>
        {
            if (!TryGetUserId(httpContext, out var userId))
            {
                return Results.Unauthorized();
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "temp_uploads", $"{userId}_{request.FileName}");
            if (!File.Exists(filePath))
            {
                return Results.BadRequest(new { message = "Uploaded file not found. Please upload again." });
            }

            try
            {
                // We need to wrap the file back into an IFormFile-like structure for the service or just pass the stream
                // To keep the service interface clean and reusable, let's use a physical file stream
                using var stream = File.OpenRead(filePath);
                var formFile = new FormFile(stream, 0, stream.Length, "file", request.FileName);
                
                var result = await importService.ProcessImportAsync(userId, formFile, request.Mapping);

                // Cleanup
                File.Delete(filePath);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        importGroup.MapGet("/history", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("ImportEndpoints");
            if (!TryGetUserId(httpContext, out var userId))
            {
                logger.LogWarning("Could not retrieve user ID from token when fetching import history.");
                return Results.Unauthorized();
            }

            logger.LogInformation("Fetching import history for User {UserId}.", userId);
            var history = await db.ImportJobs
                .Where(j => j.UserId == userId)
                .OrderByDescending(j => j.StartedAt)
                .ToListAsync();
            return Results.Ok(history);
        });
    }

    public record ProcessImportRequest(string FileName, ColumnMappingDto Mapping);

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