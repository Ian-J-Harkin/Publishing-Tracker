using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models.Dtos;
using System.Security.Claims;

namespace PublishingTracker.Api.Features.Import;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this WebApplication app)
    {
        var importGroup = app.MapGroup("/api/import").RequireAuthorization();

        importGroup.MapPost("/upload", async ([FromServices] PublishingTrackerDbContext db, IFormFile file, HttpContext httpContext, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("ImportEndpoints");
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

            // Extract headers for mapping
            var headers = new List<string>();
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                var firstLine = await reader.ReadLineAsync();
                if (!string.IsNullOrEmpty(firstLine))
                {
                    headers = firstLine.Split(',').Select(h => h.Trim('"', ' ')).ToList();
                }
            }

            return Results.Ok(new { fileName = file.FileName, headers });
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