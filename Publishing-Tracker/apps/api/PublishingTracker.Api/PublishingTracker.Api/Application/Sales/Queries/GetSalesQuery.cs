using MediatR;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Api.Models;
using PublishingTracker.Domain.Dtos;
using PublishingTracker.Application.Common.Interfaces;

namespace PublishingTracker.Application.Sales.Queries;

public record GetSalesQuery(int? BookId, int? PlatformId, DateTime? StartDate, DateTime? EndDate) : IRequest<Result<List<SaleDto>>>;

public class GetSalesQueryHandler : IRequestHandler<GetSalesQuery, Result<List<SaleDto>>>
{
    private readonly PublishingTrackerDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSalesQueryHandler(PublishingTrackerDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<SaleDto>>> Handle(GetSalesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Result<List<SaleDto>>.Failure("Unauthorized");

        var sales = await _context.Sales
            .Include(s => s.Book)
            .Include(s => s.Platform)
            .ForUser(userId.Value)
            .FilterByBook(request.BookId)
            .FilterByPlatform(request.PlatformId)
            .InDateRange(request.StartDate, request.EndDate)
            .OrderByDescending(s => s.SaleDate)
            .Select(s => s.ToDto())
            .ToListAsync(cancellationToken);

        return Result<List<SaleDto>>.Success(sales);
    }
}
