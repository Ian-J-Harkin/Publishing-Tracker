using PublishingTracker.Domain.Dtos;

namespace PublishingTracker.Application.Common.Interfaces;

public record ParsedSaleRecord(
    string BookTitle,
    string PlatformName,
    string SaleDateStr,
    string QuantityStr,
    string UnitPriceStr,
    string RoyaltyStr,
    string? RevenueStr,
    string? CurrencyStr,
    string? OrderId);

public interface ICsvReader
{
    IAsyncEnumerable<ParsedSaleRecord> ReadCsvAsync(Stream fileStream, ColumnMappingDto mapping, CancellationToken cancellationToken = default);
}
