using MediatR;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Domain.Dtos;
using PublishingTracker.Application.Common.Extensions;

namespace PublishingTracker.Application.Dashboard.Queries;

public class GetDashboardDataQueryHandler : IRequestHandler<GetDashboardDataQuery, DashboardDataDto>
{
    private readonly IApplicationDbContext _db;

    public GetDashboardDataQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardDataDto> Handle(GetDashboardDataQuery request, CancellationToken cancellationToken)
    {
        var userId = request.UserId;
        
        // 1. Summary with Currency Grouping
        var revenueByCurrency = await _db.Sales
            .ForUser(userId)
            .GroupBy(s => s.Currency)
            .Select(g => new CurrencyTotalDto { Currency = g.Key, TotalAmount = g.Sum(s => s.Revenue) })
            .ToListAsync(cancellationToken);

        var totalBooksPublished = await _db.Books.ForUser(userId).CountAsync(cancellationToken);
        var totalSalesTransactions = await _db.Sales.ForUser(userId).CountAsync(cancellationToken);

        var topBook = await _db.Sales
            .ForUser(userId)
            .GroupBy(s => s.Book.Title)
            .Select(g => new { Title = g.Key, Revenue = g.Sum(s => s.BaseCurrencyRevenue) })
            .OrderByDescending(x => x.Revenue)
            .FirstOrDefaultAsync(cancellationToken);

        var topPlatform = await _db.Sales
            .ForUser(userId)
            .GroupBy(s => s.Platform.Name)
            .Select(g => new { Name = g.Key, Revenue = g.Sum(s => s.BaseCurrencyRevenue) })
            .OrderByDescending(x => x.Revenue)
            .FirstOrDefaultAsync(cancellationToken);

        var summary = new DashboardSummaryDto
        {
            RevenueByCurrency = revenueByCurrency,
            TotalBooksPublished = totalBooksPublished,
            TotalSalesTransactions = totalSalesTransactions,
            TopPerformingBook = topBook?.Title ?? "N/A",
            TopPerformingPlatform = topPlatform?.Name ?? "N/A"
        };

        // 2. YOY
        var currentYear = DateTime.UtcNow.Year;
        var currentYearRevenue = await _db.Sales
            .ForUser(userId)
            .Where(s => s.SaleDate.Year == currentYear)
            .SumAsync(s => s.BaseCurrencyRevenue, cancellationToken);
            
        var lastYearRevenue = await _db.Sales
            .ForUser(userId)
            .Where(s => s.SaleDate.Year == currentYear - 1)
            .SumAsync(s => s.BaseCurrencyRevenue, cancellationToken);

        var yoy = new
        {
            currentYearRevenue,
            lastYearRevenue,
            growth = lastYearRevenue > 0 ? (double)(currentYearRevenue - lastYearRevenue) / (double)lastYearRevenue : 0
        };

        // 3. Seasonal
        var seasonal = await _db.Sales
            .ForUser(userId)
            .Where(s => s.SaleDate.Year == currentYear)
            .GroupBy(s => s.SaleDate.Month)
            .Select(g => new { Month = g.Key, TotalRevenue = g.Sum(s => s.BaseCurrencyRevenue) })
            .OrderBy(x => x.Month)
            .ToListAsync(cancellationToken);

        return new DashboardDataDto
        {
            Summary = summary,
            YoY = yoy,
            Seasonal = seasonal
        };
    }
}
