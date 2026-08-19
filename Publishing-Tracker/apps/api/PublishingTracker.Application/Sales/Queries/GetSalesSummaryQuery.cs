using MediatR;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Interfaces;


using PublishingTracker.Domain.Dtos;
using PublishingTracker.Application.Common.Interfaces;

namespace PublishingTracker.Application.Sales.Queries;

public record GetSalesSummaryQuery(DateTime? StartDate, DateTime? EndDate) : IRequest<Result<SalesSummaryDto>>;

public class GetSalesSummaryQueryHandler : IRequestHandler<GetSalesSummaryQuery, Result<SalesSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSalesSummaryQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SalesSummaryDto>> Handle(GetSalesSummaryQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Result<SalesSummaryDto>.Failure("Unauthorized");

        var summary = await _context.Sales
            .ForUser(userId.Value)
            .InDateRange(request.StartDate, request.EndDate)
            .GroupBy(s => 1)
            .Select(g => new SalesSummaryDto
            {
                TotalRevenue = g.Sum(s => s.BaseCurrencyRevenue),
                TotalUnitsSold = g.Sum(s => s.Quantity),
                AverageRoyalty = g.Count() > 0 ? g.Sum(s => s.BaseCurrencyRoyalty) / g.Count() : 0,
                SalesCount = g.Count()
            }).FirstOrDefaultAsync(cancellationToken) ?? new SalesSummaryDto();

        return Result<SalesSummaryDto>.Success(summary);
    }
}
