using MediatR;
using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Application.Common.Extensions;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Interfaces;

namespace PublishingTracker.Application.Dashboard.Queries;

public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IQueryExecutor _queries;

    public GetDashboardSummaryQueryHandler(IApplicationDbContext db, IQueryExecutor queries)
    {
        _db = db;
        _queries = queries;
    }

    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var userId = request.UserId;
        
        var revenueByCurrency = await _queries.ToListAsync(_db.Sales
            .ForUser(userId)
            .GroupBy(s => s.Currency)
            .Select(g => new CurrencyTotalDto { Currency = g.Key, TotalAmount = g.Sum(s => s.Revenue) })
            , cancellationToken);

        return new DashboardSummaryDto
        {
            RevenueByCurrency = revenueByCurrency,
            TotalBooksPublished = (await _queries.ToListAsync(_db.Books.ForUser(userId), cancellationToken)).Count,
            TotalSalesTransactions = (await _queries.ToListAsync(_db.Sales.ForUser(userId), cancellationToken)).Count,
            TopPerformingBook = "N/A", // This lightweight summary endpoint previously didn't calculate top performers
            TopPerformingPlatform = "N/A"
        };
    }
}
