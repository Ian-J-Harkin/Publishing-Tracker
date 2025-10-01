using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models.Dtos;

namespace PublishingTracker.Api.Extensions;

public static class ImportRoutes
{
    public static void MapImportRoutes(this WebApplication app)
    {
        var importGroup = app.MapGroup("/api/import").RequireAuthorization();

        importGroup.MapPost("/upload", async ([FromServices] PublishingTrackerDbContext db, IFormFile file, HttpContext httpContext) =>
        {
            // This is a placeholder for the upload logic.
            // In a real application, you would save the file and return a file ID.
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream); // Example async operation

            // Save to DB or storage here...
            return Results.Ok(new { fileName = file.FileName });
        });

        importGroup.MapPost("/process", async ([FromServices] PublishingTrackerDbContext db, ColumnMappingDto mapping, HttpContext httpContext) =>
        {
            // This is a placeholder for the processing logic.
            // In a real application, you would retrieve the file, parse it using the mapping,
            // and create the sale records.
            await Task.CompletedTask; // Example dummy async operation
            return Results.Ok(new { message = "Import processing started." });
        });

        importGroup.MapGet("/history", async ([FromServices] PublishingTrackerDbContext db, HttpContext httpContext) =>
        {
            var userId = int.Parse(httpContext.User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
            var history = await db.ImportJobs
                .Where(j => j.UserId == userId)
                .OrderByDescending(j => j.StartedAt)
                .ToListAsync();
            return Results.Ok(history);
        });
    }
}