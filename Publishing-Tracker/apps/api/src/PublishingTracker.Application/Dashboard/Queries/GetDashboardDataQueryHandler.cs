using MediatR;
using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Application.Common.Extensions;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Interfaces;

namespace PublishingTracker.Application.Dashboard.Queries;

public class GetDashboardDataQueryHandler : IRequestHandler<GetDashboardDataQuery, DashboardDataDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IQueryExecutor _queries;

    public GetDashboardDataQueryHandler(IApplicationDbContext db, IQueryExecutor queries)
    {
        _db = db;
        _queries = queries;
    }

    public async Task<DashboardDataDto> Handle(GetDashboardDataQuery request, CancellationToken cancellationToken)
    {
        var userId = request.UserId;
        
        // 1. Summary with Currency Grouping
        var revenueByCurrency = await _queries.ToListAsync(_db.Sales
            .ForUser(userId)
            .GroupBy(s => s.Currency)
            .Select(g => new CurrencyTotalDto { Currency = g.Key, TotalAmount = g.Sum(s => s.Revenue) })
            , cancellationToken);

        var totalBooksPublished = (await _queries.ToListAsync(_db.Books.ForUser(userId), cancellationToken)).Count;
        var totalSalesTransactions = (await _queries.ToListAsync(_db.Sales.ForUser(userId), cancellationToken)).Count;

        var topBook = (await _queries.ToListAsync(_db.Sales
            .ForUser(userId)
            .GroupBy(s => s.Book.Title)
            .Select(g => new { Title = g.Key, Revenue = g.Sum(s => s.BaseCurrencyRevenue) })
            .OrderByDescending(x => x.Revenue)
            , cancellationToken)).FirstOrDefault();

        var topPlatform = (await _queries.ToListAsync(_db.Sales
            .ForUser(userId)
            .GroupBy(s => s.Platform.Name)
            .Select(g => new { Name = g.Key, Revenue = g.Sum(s => s.BaseCurrencyRevenue) })
            .OrderByDescending(x => x.Revenue)
            , cancellationToken)).FirstOrDefault();

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
        var currentYearRevenue = (await _queries.ToListAsync(_db.Sales
            .ForUser(userId)
            .Where(s => s.SaleDate.Year == currentYear)
            .Select(s => s.BaseCurrencyRevenue), cancellationToken)).Sum();
            
        var lastYearRevenue = (await _queries.ToListAsync(_db.Sales
            .ForUser(userId)
            .Where(s => s.SaleDate.Year == currentYear - 1)
            .Select(s => s.BaseCurrencyRevenue), cancellationToken)).Sum();

        var yoy = new
        {
            currentYearRevenue,
            lastYearRevenue,
            growth = lastYearRevenue > 0 ? (double)(currentYearRevenue - lastYearRevenue) / (double)lastYearRevenue : 0
        };

        // 3. Seasonal
        var seasonal = await _queries.ToListAsync(_db.Sales
            .ForUser(userId)
            .Where(s => s.SaleDate.Year == currentYear)
            .GroupBy(s => s.SaleDate.Month)
            .Select(g => new { Month = g.Key, TotalRevenue = g.Sum(s => s.BaseCurrencyRevenue) })
            .OrderBy(x => x.Month)
            , cancellationToken);

        return new DashboardDataDto
        {
            Summary = summary,
            YoY = yoy,
            Seasonal = seasonal
        };
    }
}
