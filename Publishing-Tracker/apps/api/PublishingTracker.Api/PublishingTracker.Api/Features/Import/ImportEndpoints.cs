using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;
using PublishingTracker.Api.Services;

namespace PublishingTracker.Api.Features.Import;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this WebApplication app)
    {
        var importGroup = app.MapGroup("/api/import").RequireAuthorization().DisableAntiforgery();

        // ── Upload: saves file & returns preview (unchanged) ──────
        importGroup.MapPost("/upload", async ([FromServices] PublishingTrackerDbContext db, IFormFile file, HttpContext httpContext, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("ImportEndpoints");
            try
            {
                if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                    return errorResult!;

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

        // ── Process: enqueues to background service, returns 202 ──
        importGroup.MapPost("/process", async (
            [FromServices] PublishingTrackerDbContext db,
            [FromServices] ImportBackgroundService backgroundService,
            ProcessImportRequest request,
            HttpContext httpContext) =>
        {
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "temp_uploads", $"{userId}_{request.FileName}");
            if (!File.Exists(filePath))
            {
                return Results.BadRequest(new { message = "Uploaded file not found. Please upload again." });
            }

            // Create the job record immediately so the client has an ID to poll
            var job = new ImportJob
            {
                UserId = userId,
                FileName = request.FileName,
                Status = "Queued",
                StartedAt = DateTime.UtcNow,
                RecordsProcessed = 0,
                RecordsSuccessful = 0,
                RecordsFailed = 0
            };

            db.ImportJobs.Add(job);
            await db.SaveChangesAsync();

            // Enqueue for background processing (non-blocking)
            await backgroundService.EnqueueAsync(new ImportRequest
            {
                UserId = userId,
                JobId = job.Id,
                FilePath = filePath,
                FileName = request.FileName,
                Mapping = request.Mapping
            });

            // Return 202 Accepted with the job ID for status polling
            return Results.Accepted($"/api/import/status/{job.Id}", job.ToDto());
        });

        // ── Status: poll a specific import job by ID ──────────────
        importGroup.MapGet("/status/{jobId}", async (
            [FromServices] PublishingTrackerDbContext db,
            HttpContext httpContext,
            int jobId) =>
        {
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            var job = await db.ImportJobs
                .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId);

            if (job == null)
                return Results.NotFound("Import job not found.");

            return Results.Ok(job.ToDto());
        });

        // ── History: all import jobs for the user ─────────────────
        importGroup.MapGet("/history", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("ImportEndpoints");
            if (!httpContext.TryGetUserId(out var userId, out var errorResult))
                return errorResult!;

            logger.LogInformation("Fetching import history for User {UserId}.", userId);
            var history = await db.ImportJobs
                .ForUser(userId)
                .OrderByDescending(j => j.StartedAt)
                .ToListAsync();
            return Results.Ok(history);
        });
    }

    public record ProcessImportRequest(string FileName, ColumnMappingDto Mapping);
}