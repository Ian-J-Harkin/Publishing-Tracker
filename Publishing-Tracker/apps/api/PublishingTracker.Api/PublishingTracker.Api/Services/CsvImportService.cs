using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;
using System.Globalization;

namespace PublishingTracker.Api.Services;

public class CsvImportService : ICsvImportService
{
    private readonly PublishingTrackerDbContext _db;
    private readonly ILogger<CsvImportService> _logger;

    public CsvImportService(PublishingTrackerDbContext db, ILogger<CsvImportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ImportJobDto> ProcessImportAsync(int userId, IFormFile file, ColumnMappingDto mapping)
    {
        var job = new ImportJob
        {
            UserId = userId,
            FileName = file.FileName,
            Status = "Processing",
            StartedAt = DateTime.UtcNow,
            RecordsProcessed = 0,
            RecordsSuccessful = 0,
            RecordsFailed = 0
        };

        _db.ImportJobs.Add(job);
        await _db.SaveChangesAsync();

        try
        {
            // Pre-fetch user's books and platforms for performance and lookup
            var userBooks = await _db.Books.Where(b => b.UserId == userId).ToListAsync();
            var platforms = await _db.Platforms.ToListAsync();

            using var reader = new StreamReader(file.OpenReadStream());
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null
            });

            await csv.ReadAsync();
            csv.ReadHeader();

            while (await csv.ReadAsync())
            {
                job.RecordsProcessed++;
                try
                {
                    var bookTitle = csv.GetField(mapping.BookTitle);
                    var platformName = csv.GetField(mapping.Platform);
                    var saleDateStr = csv.GetField(mapping.SaleDate);
                    var quantityStr = csv.GetField(mapping.Quantity);
                    var unitPriceStr = csv.GetField(mapping.UnitPrice);
                    var royaltyStr = csv.GetField(mapping.Royalty);
                    var revenueStr = !string.IsNullOrEmpty(mapping.Revenue) ? csv.GetField(mapping.Revenue) : null;
                    var currency = (!string.IsNullOrEmpty(mapping.Currency) && !string.IsNullOrEmpty(csv.GetField(mapping.Currency))) 
                        ? csv.GetField(mapping.Currency) 
                        : mapping.DefaultCurrency;
                    var orderId = !string.IsNullOrEmpty(mapping.OrderId) ? csv.GetField(mapping.OrderId) : null;

                    // 1. Resolve Book (Create if missing as per US-016)
                    if (string.IsNullOrWhiteSpace(bookTitle))
                    {
                        job.RecordsFailed++;
                        job.ErrorLog += $"Row {job.RecordsProcessed}: Book title is required but was empty or missing.\\n";
                        _logger.LogWarning("Import row {RowNum} skipped: Book title is empty", job.RecordsProcessed);
                        continue; // Skip this record
                    }

                    var book = userBooks.FirstOrDefault(b => b.Title.Equals(bookTitle, StringComparison.OrdinalIgnoreCase));
                    if (book == null)
                    {
                        book = new Book { UserId = userId, Title = bookTitle!, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                        _db.Books.Add(book);
                        await _db.SaveChangesAsync(); // Save to get ID
                        userBooks.Add(book);
                    }

                    // 2. Resolve Platform - Create if missing (Smart Resolve)
                    var platform = platforms.FirstOrDefault(p => p.Name.Equals(platformName, StringComparison.OrdinalIgnoreCase));
                    if (platform == null)
                    {
                        if (string.IsNullOrWhiteSpace(platformName))
                        {
                           // Fallback to "Unknown" or skip? Let's skip empty platforms for now but log it.
                           job.RecordsFailed++;
                           job.ErrorLog += $"Row {job.RecordsProcessed}: Platform name is empty.\n";
                           continue;
                        }

                        // Create new platform on the fly
                        platform = new Platform { Name = platformName };
                        _db.Platforms.Add(platform);
                        await _db.SaveChangesAsync();
                        platforms.Add(platform);
                        _logger.LogInformation("Auto-created new platform '{PlatformName}' during import.", platformName);
                    }

                    // 3. Deduplication (US-017 troubleshooting/reliability)
                    if (!string.IsNullOrEmpty(orderId))
                    {
                        var exists = await _db.Sales.AnyAsync(s => s.OrderId == orderId && s.BookId == book.Id);
                        if (exists)
                        {
                            job.RecordsFailed++;
                            job.ErrorLog += $"Row {job.RecordsProcessed}: Duplicate order ID '{orderId}' for book '{bookTitle}' - record already exists in database.\n";
                            _logger.LogInformation("Skipping duplicate OrderId: {OrderId}", orderId);
                            continue; 
                        }
                    }

                    // 4. Parse Values
                    var sale = new Sale
                    {
                        BookId = book.Id,
                        PlatformId = platform.Id,
                        SaleDate = DateTime.TryParse(saleDateStr, out var d) ? d : DateTime.UtcNow,
                        Quantity = int.TryParse(quantityStr, out var q) ? q : 0,
                        UnitPrice = decimal.TryParse(unitPriceStr, out var up) ? up : 0m,
                        Royalty = decimal.TryParse(royaltyStr, out var r) ? r : 0m,
                        Revenue = decimal.TryParse(revenueStr, out var rev) ? rev : (decimal.TryParse(unitPriceStr, out var up2) ? up2 * (int.TryParse(quantityStr, out var q2) ? q2 : 0) : 0m),
                        Currency = string.IsNullOrEmpty(currency) ? mapping.DefaultCurrency : currency,
                        OrderId = orderId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _db.Sales.Add(sale);
                    job.RecordsSuccessful++;
                }
                catch (Exception ex)
                {
                    job.RecordsFailed++;
                    job.ErrorLog += $"Row {job.RecordsProcessed}: {ex.Message}\n";
                }

                // Batch save every 100 records for efficiency
                if (job.RecordsProcessed % 100 == 0)
                {
                    await _db.SaveChangesAsync();
                }
            }

            job.Status = "Completed";
            job.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            job.Status = "Failed";
            job.ErrorLog += $"Critical Error: {ex.Message}";
            _logger.LogError(ex, "CSV Import failed for job {JobId}", job.Id);
        }

        await _db.SaveChangesAsync();

        return new ImportJobDto
        {
            Id = job.Id,
            FileName = job.FileName,
            Status = job.Status,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            RecordsProcessed = job.RecordsProcessed,
            RecordsSuccessful = job.RecordsSuccessful,
            RecordsFailed = job.RecordsFailed,
            ErrorLog = job.ErrorLog
        };
    }
}
