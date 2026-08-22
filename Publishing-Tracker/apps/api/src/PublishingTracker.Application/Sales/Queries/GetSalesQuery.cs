using MediatR;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Interfaces;


using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Application.Common.Interfaces;

namespace PublishingTracker.Application.Sales.Queries;

public record GetSalesQuery(int? BookId, int? PlatformId, DateTime? StartDate, DateTime? EndDate) : IRequest<Result<List<SaleDto>>>;

public class GetSalesQueryHandler : IRequestHandler<GetSalesQuery, Result<List<SaleDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IQueryExecutor _queries;
    private readonly ICurrentUserService _currentUserService;

    public GetSalesQueryHandler(IApplicationDbContext context, IQueryExecutor queries, ICurrentUserService currentUserService)
    {
        _context = context;
        _queries = queries;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<SaleDto>>> Handle(GetSalesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Result<List<SaleDto>>.Failure("Unauthorized");

        var sales = await _queries.ToListAsync(_context.Sales
            .ForUser(userId.Value)
            .FilterByBook(request.BookId)
            .FilterByPlatform(request.PlatformId)
            .InDateRange(request.StartDate, request.EndDate)
            .OrderByDescending(s => s.SaleDate)
            .Select(s => s.ToDto())
            , cancellationToken);

        return Result<List<SaleDto>>.Success(sales);
    }
}
