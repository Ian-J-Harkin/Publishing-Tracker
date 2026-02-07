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
                logger.LogWarning("Could not retrieve user ID from token during file upload.");
                return Results.Unauthorized();
            }

            logger.LogInformation("User {UserId} uploaded file {FileName}.", userId, file.FileName);
            
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

        importGroup.MapPost("/process", async ([FromServices] PublishingTrackerDbContext db, ColumnMappingDto mapping, HttpContext httpContext, [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("ImportEndpoints");
            if (!TryGetUserId(httpContext, out var userId))
            {
                logger.LogWarning("Could not retrieve user ID from token during import processing.");
                return Results.Unauthorized();
            }

            logger.LogInformation("User {UserId} started processing an import.", userId);
            // This is a placeholder for the processing logic.
            // In a real application, you would retrieve the file, parse it using the mapping,
            // and create the sale records.
            await Task.CompletedTask; // Example dummy async operation
            return Results.Ok(new { message = "Import processing started." });
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