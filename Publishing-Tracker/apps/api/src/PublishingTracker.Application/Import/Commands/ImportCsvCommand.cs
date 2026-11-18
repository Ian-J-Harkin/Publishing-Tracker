using MediatR;
using Microsoft.Extensions.Logging;
using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Domain;
using PublishingTracker.Domain.Entities;

namespace PublishingTracker.Application.Import.Commands;

public record ImportCsvCommand(int UserId, int JobId, string FilePath, ColumnMappingDto Mapping) : IRequest<Result<ImportJobDto>>;

public class ImportCsvCommandHandler : IRequestHandler<ImportCsvCommand, Result<ImportJobDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IQueryExecutor _queries;
    private readonly ICsvReader _csvReader;
    private readonly ILogger<ImportCsvCommandHandler> _logger;
    private readonly ICurrencyConverterService _currencyConverterService;

    public ImportCsvCommandHandler(
        IApplicationDbContext db, 
        IQueryExecutor queries,
        ICsvReader csvReader, 
        ILogger<ImportCsvCommandHandler> logger,
        ICurrencyConverterService currencyConverterService)
    {
        _db = db;
        _queries = queries;
        _csvReader = csvReader;
        _logger = logger;
        _currencyConverterService = currencyConverterService;
    }

    public async Task<Result<ImportJobDto>> Handle(ImportCsvCommand request, CancellationToken cancellationToken)
    {
        var job = await _queries.FirstOrDefaultAsync(_db.ImportJobs.Where(j => j.Id == request.JobId), cancellationToken);
        if (job == null)
        {
            _logger.LogError("Import job {JobId} not found in database.", request.JobId);
            return Result<ImportJobDto>.Failure("Job not found");
        }

        try
        {
            // Pre-fetch user's books and platforms
            var userBooks = await _queries.ToListAsync(_db.Books.Where(b => b.UserId == request.UserId), cancellationToken);
            var platforms = await _queries.ToListAsync(_db.Platforms, cancellationToken);

            using var fileStream = new FileStream(request.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var parsedRecords = _csvReader.ReadCsvAsync(fileStream, request.Mapping, cancellationToken);

            await foreach (var record in parsedRecords)
            {
                job.IncrementProcessed();
                try
                {
                    // 1. Resolve Book
                    if (string.IsNullOrWhiteSpace(record.BookTitle))
                    {
                        job.IncrementFailed($"Row {job.RecordsProcessed}: Book title is required but was empty or missing.");
                        _logger.LogWarning("Import row {RowNum} skipped: Book title is empty", job.RecordsProcessed);
                        continue; 
                    }

                    var book = userBooks.FirstOrDefault(b => b.Title.Equals(record.BookTitle, StringComparison.OrdinalIgnoreCase));
                    if (book == null)
                    {
                        book = new Book(request.UserId, record.BookTitle, "Unknown Author");
                        _db.Add(book);
                        await _db.SaveChangesAsync(cancellationToken); 
                        userBooks.Add(book);
                    }

                    // 2. Resolve Platform
                    var platform = platforms.FirstOrDefault(p => p.Name.Equals(record.PlatformName, StringComparison.OrdinalIgnoreCase));
                    if (platform == null)
                    {
                        if (string.IsNullOrWhiteSpace(record.PlatformName))
                        {
                           job.IncrementFailed($"Row {job.RecordsProcessed}: Platform name is empty.");
                           continue;
                        }

                        platform = Platform.Create(record.PlatformName, record.PlatformName, 0m);
                        _db.Add(platform);
                        await _db.SaveChangesAsync(cancellationToken);
                        platforms.Add(platform);
                        _logger.LogInformation("Auto-created new platform '{PlatformName}' during import.", record.PlatformName);
                    }

                    // 3. Deduplication
                    if (!string.IsNullOrWhiteSpace(record.OrderId))
                    {
                        var exists = await _queries.AnyAsync(_db.Sales.Where(s => s.OrderId == record.OrderId && s.BookId == book.Id), cancellationToken);
                        if (exists)
                        {
                            job.IncrementFailed($"Row {job.RecordsProcessed}: Duplicate order ID '{record.OrderId}' for book '{record.BookTitle}' - record already exists.");
                            _logger.LogInformation("Skipping duplicate OrderId: {OrderId}", record.OrderId);
                            continue; 
                        }
                    }

                    // 4. Parse Values
                    var currency = (!string.IsNullOrEmpty(request.Mapping.Currency) && !string.IsNullOrEmpty(record.CurrencyStr)) 
                        ? record.CurrencyStr 
                        : request.Mapping.DefaultCurrency;

                    var finalCurrency = string.IsNullOrEmpty(currency) ? request.Mapping.DefaultCurrency : currency;
                    var exchangeRate = _currencyConverterService.GetExchangeRate(finalCurrency, "USD");

                    var sale = Sale.Create(
                        book.Id,
                        platform.Id,
                        DateTime.TryParse(record.SaleDateStr, out var d) ? d : DateTime.UtcNow,
                        int.TryParse(record.QuantityStr, out var q) ? q : 1,
                        decimal.TryParse(record.UnitPriceStr, out var up) ? up : 0m,
                        decimal.TryParse(record.RoyaltyStr, out var r) ? r : 0m,
                        platform.CommissionRate,
                        exchangeRate,
                        finalCurrency,
                        record.OrderId,
                        decimal.TryParse(record.RevenueStr, out var rev) ? (decimal?)rev : null
                    );

                    _db.Add(sale);
                    job.IncrementSuccessful();
                }
                catch (Exception ex)
                {
                    job.IncrementFailed($"Row {job.RecordsProcessed}: {ex.Message}");
                }

                if (job.RecordsProcessed % 100 == 0)
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            job.MarkCompleted();
        }
        catch (OperationCanceledException)
        {
            job.MarkCancelled();
            _logger.LogWarning("Import job {JobId} was cancelled.", request.JobId);
        }
        catch (Exception ex)
        {
            job.MarkFailed($"Critical Error: {ex.Message}");
            _logger.LogError(ex, "CSV Import failed for job {JobId}", request.JobId);
        }

        await _db.SaveChangesAsync(cancellationToken);
        
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

        return Result<ImportJobDto>.Success(job.ToDto());
    }
}
