using MediatR;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Domain.Dtos;
using PublishingTracker.Application.Common.Extensions;

namespace PublishingTracker.Application.Dashboard.Queries;

public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IApplicationDbContext _db;

    public GetDashboardSummaryQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var userId = request.UserId;
        
        var revenueByCurrency = await _db.Sales
            .ForUser(userId)
            .GroupBy(s => s.Currency)
            .Select(g => new CurrencyTotalDto { Currency = g.Key, TotalAmount = g.Sum(s => s.Revenue) })
            .ToListAsync(cancellationToken);

        return new DashboardSummaryDto
        {
            RevenueByCurrency = revenueByCurrency,
            TotalBooksPublished = await _db.Books.ForUser(userId).CountAsync(cancellationToken),
            TotalSalesTransactions = await _db.Sales.ForUser(userId).CountAsync(cancellationToken),
            TopPerformingBook = "N/A", // This lightweight summary endpoint previously didn't calculate top performers
            TopPerformingPlatform = "N/A"
        };
    }
}
