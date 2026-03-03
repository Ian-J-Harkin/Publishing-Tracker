using System.Threading.Channels;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Api.Models;
using System.Globalization;

namespace PublishingTracker.Api.Services;

/// <summary>
/// A hosted BackgroundService that processes CSV imports off the HTTP request thread.
/// Uses <see cref="Channel{T}"/> (producer/consumer pattern) so the API endpoint can enqueue
/// work and immediately return 202 Accepted. Demonstrates multi-threading, custom events,
/// and generic delegates in C#.
/// </summary>
public class ImportBackgroundService : BackgroundService
{
    private readonly Channel<ImportRequest> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImportBackgroundService> _logger;

    // ── Custom Events ──────────────────────────────────────────────
    /// <summary>Raised after each batch of rows is processed.</summary>
    public event EventHandler<ImportProgressEventArgs>? ProgressChanged;

    /// <summary>Raised when an import job finishes (success or failure).</summary>
    public event EventHandler<ImportProgressEventArgs>? ImportCompleted;

    // ── Generic Delegate ───────────────────────────────────────────
    /// <summary>
    /// A generic delegate for transforming an import row before persistence.
    /// Can be replaced or extended without modifying this class.
    /// </summary>
    public delegate Sale RowTransformer(CsvReader csv, Models.Dtos.ColumnMappingDto mapping, Book book, Platform platform);

    public ImportBackgroundService(
        Channel<ImportRequest> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<ImportBackgroundService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Enqueues an import request. Called from the API endpoint.
    /// </summary>
    public async ValueTask EnqueueAsync(ImportRequest request, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(request, ct);
        _logger.LogInformation("Import job {JobId} enqueued for background processing.", request.JobId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ImportBackgroundService started. Waiting for import requests...");

        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessImportAsync(request, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing import job {JobId}.", request.JobId);
            }
        }
    }

    private async Task ProcessImportAsync(ImportRequest request, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PublishingTrackerDbContext>();

        var job = await db.ImportJobs.FindAsync(new object[] { request.JobId }, ct);
        if (job == null)
        {
            _logger.LogError("Import job {JobId} not found in database.", request.JobId);
            return;
        }

        try
        {
            // Pre-fetch user's books and platforms
            var userBooks = await db.Books.ForUser(request.UserId).ToListAsync(ct);
            var platforms = await db.Platforms.ToListAsync(ct);

            using var reader = new StreamReader(request.FilePath);
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
                ct.ThrowIfCancellationRequested();

                job.RecordsProcessed++;
                try
                {
                    var bookTitle = csv.GetField(request.Mapping.BookTitle);
                    var platformName = csv.GetField(request.Mapping.Platform);
                    var saleDateStr = csv.GetField(request.Mapping.SaleDate);
                    var quantityStr = csv.GetField(request.Mapping.Quantity);
                    var unitPriceStr = csv.GetField(request.Mapping.UnitPrice);
                    var royaltyStr = csv.GetField(request.Mapping.Royalty);
                    var revenueStr = !string.IsNullOrEmpty(request.Mapping.Revenue) ? csv.GetField(request.Mapping.Revenue) : null;
                    var currency = (!string.IsNullOrEmpty(request.Mapping.Currency) && !string.IsNullOrEmpty(csv.GetField(request.Mapping.Currency)))
                        ? csv.GetField(request.Mapping.Currency)
                        : request.Mapping.DefaultCurrency;
                    var orderId = !string.IsNullOrEmpty(request.Mapping.OrderId) ? csv.GetField(request.Mapping.OrderId) : null;

                    // 1. Resolve Book
                    if (string.IsNullOrWhiteSpace(bookTitle))
                    {
                        job.RecordsFailed++;
                        job.ErrorLog += $"Row {job.RecordsProcessed}: Book title is required but was empty or missing.\\n";
                        _logger.LogWarning("Import row {RowNum} skipped: Book title is empty", job.RecordsProcessed);
                        continue;
                    }

                    var book = userBooks.FirstOrDefault(b => b.Title.Equals(bookTitle, StringComparison.OrdinalIgnoreCase));
                    if (book == null)
                    {
                        book = new Book { UserId = request.UserId, Title = bookTitle!, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                        db.Books.Add(book);
                        await db.SaveChangesAsync(ct);
                        userBooks.Add(book);
                    }

                    // 2. Resolve Platform
                    var platform = platforms.FirstOrDefault(p => p.Name.Equals(platformName, StringComparison.OrdinalIgnoreCase));
                    if (platform == null)
                    {
                        if (string.IsNullOrWhiteSpace(platformName))
                        {
                            job.RecordsFailed++;
                            job.ErrorLog += $"Row {job.RecordsProcessed}: Platform name is empty.\n";
                            continue;
                        }

                        platform = new Platform { Name = platformName };
                        db.Platforms.Add(platform);
                        await db.SaveChangesAsync(ct);
                        platforms.Add(platform);
                        _logger.LogInformation("Auto-created new platform '{PlatformName}' during import.", platformName);
                    }

                    // 3. Deduplication
                    if (!string.IsNullOrEmpty(orderId))
                    {
                        var exists = await db.Sales.AnyAsync(s => s.OrderId == orderId && s.BookId == book.Id, ct);
                        if (exists)
                        {
                            job.RecordsFailed++;
                            job.ErrorLog += $"Row {job.RecordsProcessed}: Duplicate order ID '{orderId}' for book '{bookTitle}' - record already exists.\n";
                            _logger.LogInformation("Skipping duplicate OrderId: {OrderId}", orderId);
                            continue;
                        }
                    }

                    // 4. Parse Values & Create Sale
                    var sale = new Sale
                    {
                        BookId = book.Id,
                        PlatformId = platform.Id,
                        SaleDate = DateTime.TryParse(saleDateStr, out var d) ? d : DateTime.UtcNow,
                        Quantity = int.TryParse(quantityStr, out var q) ? q : 0,
                        UnitPrice = decimal.TryParse(unitPriceStr, out var up) ? up : 0m,
                        Royalty = decimal.TryParse(royaltyStr, out var r) ? r : 0m,
                        Revenue = decimal.TryParse(revenueStr, out var rev) ? rev : (decimal.TryParse(unitPriceStr, out var up2) ? up2 * (int.TryParse(quantityStr, out var q2) ? q2 : 0) : 0m),
                        Currency = string.IsNullOrEmpty(currency) ? request.Mapping.DefaultCurrency : currency,
                        OrderId = orderId,
                        CreatedAt = DateTime.UtcNow
                    };

                    db.Sales.Add(sale);
                    job.RecordsSuccessful++;
                }
                catch (Exception ex)
                {
                    job.RecordsFailed++;
                    job.ErrorLog += $"Row {job.RecordsProcessed}: {ex.Message}\n";
                }

                // Batch save every 100 rows
                if (job.RecordsProcessed % 100 == 0)
                {
                    await db.SaveChangesAsync(ct);

                    // Fire progress event
                    OnProgressChanged(new ImportProgressEventArgs
                    {
                        JobId = request.JobId,
                        RowsProcessed = job.RecordsProcessed,
                        RowsSuccessful = job.RecordsSuccessful,
                        RowsFailed = job.RecordsFailed
                    });
                }
            }

            job.Status = "Completed";
            job.CompletedAt = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            job.Status = "Cancelled";
            _logger.LogWarning("Import job {JobId} was cancelled.", request.JobId);
        }
        catch (Exception ex)
        {
            job.Status = "Failed";
            job.ErrorLog += $"Critical Error: {ex.Message}";
            _logger.LogError(ex, "CSV Import failed for job {JobId}", request.JobId);
        }

        await db.SaveChangesAsync(ct);

        // Cleanup temp file
        try
        {
            if (File.Exists(request.FilePath))
                File.Delete(request.FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temp file {FilePath}", request.FilePath);
        }

        // Fire completion event
        OnImportCompleted(new ImportProgressEventArgs
        {
            JobId = request.JobId,
            RowsProcessed = job.RecordsProcessed,
            RowsSuccessful = job.RecordsSuccessful,
            RowsFailed = job.RecordsFailed,
            IsComplete = true
        });

        _logger.LogInformation(
            "Import job {JobId} finished. Status: {Status}, Processed: {Processed}, Success: {Success}, Failed: {Failed}",
            request.JobId, job.Status, job.RecordsProcessed, job.RecordsSuccessful, job.RecordsFailed);
    }

    protected virtual void OnProgressChanged(ImportProgressEventArgs e)
        => ProgressChanged?.Invoke(this, e);

    protected virtual void OnImportCompleted(ImportProgressEventArgs e)
        => ImportCompleted?.Invoke(this, e);
}
