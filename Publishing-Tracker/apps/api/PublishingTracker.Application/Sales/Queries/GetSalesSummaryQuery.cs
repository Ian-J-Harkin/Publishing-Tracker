using MediatR;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Interfaces;


using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Application.Common.Interfaces;

namespace PublishingTracker.Application.Sales.Queries;

public record GetSalesSummaryQuery(DateTime? StartDate, DateTime? EndDate) : IRequest<Result<SalesSummaryDto>>;

public class GetSalesSummaryQueryHandler : IRequestHandler<GetSalesSummaryQuery, Result<SalesSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IQueryExecutor _queries;
    private readonly ICurrentUserService _currentUserService;

    public GetSalesSummaryQueryHandler(IApplicationDbContext context, IQueryExecutor queries, ICurrentUserService currentUserService)
    {
        _context = context;
        _queries = queries;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SalesSummaryDto>> Handle(GetSalesSummaryQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Result<SalesSummaryDto>.Failure("Unauthorized");

        var sales = await _queries.ToListAsync(_context.Sales
            .ForUser(userId.Value)
            .InDateRange(request.StartDate, request.EndDate), cancellationToken);

        var summary = sales.Any()
            ? new SalesSummaryDto
            {
                TotalRevenue = sales.Sum(s => s.BaseCurrencyRevenue),
                TotalUnitsSold = sales.Sum(s => s.Quantity),
                AverageRoyalty = sales.Sum(s => s.BaseCurrencyRoyalty) / sales.Count,
                SalesCount = sales.Count
            }
            : new SalesSummaryDto();

        return Result<SalesSummaryDto>.Success(summary);
    }
}
