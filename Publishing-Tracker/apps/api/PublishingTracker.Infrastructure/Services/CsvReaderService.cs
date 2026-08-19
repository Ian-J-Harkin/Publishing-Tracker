using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Domain.Dtos;
using System.Runtime.CompilerServices;

namespace PublishingTracker.Infrastructure.Services;

public class CsvReaderService : ICsvReader
{
    public async IAsyncEnumerable<ParsedSaleRecord> ReadCsvAsync(Stream fileStream, ColumnMappingDto mapping, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(fileStream);
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
            cancellationToken.ThrowIfCancellationRequested();

            var bookTitle = csv.GetField(mapping.BookTitle) ?? string.Empty;
            var platformName = csv.GetField(mapping.Platform) ?? string.Empty;
            var saleDateStr = csv.GetField(mapping.SaleDate) ?? string.Empty;
            var quantityStr = csv.GetField(mapping.Quantity) ?? string.Empty;
            var unitPriceStr = csv.GetField(mapping.UnitPrice) ?? string.Empty;
            var royaltyStr = csv.GetField(mapping.Royalty) ?? string.Empty;
            var revenueStr = !string.IsNullOrWhiteSpace(mapping.Revenue) ? csv.GetField(mapping.Revenue) : null;
            var currencyStr = !string.IsNullOrWhiteSpace(mapping.Currency) ? csv.GetField(mapping.Currency) : null;
            var orderId = !string.IsNullOrWhiteSpace(mapping.OrderId) ? csv.GetField(mapping.OrderId) : null;

            yield return new ParsedSaleRecord(
                bookTitle,
                platformName,
                saleDateStr,
                quantityStr,
                unitPriceStr,
                royaltyStr,
                revenueStr,
                currencyStr,
                orderId
            );
        }
    }
}
