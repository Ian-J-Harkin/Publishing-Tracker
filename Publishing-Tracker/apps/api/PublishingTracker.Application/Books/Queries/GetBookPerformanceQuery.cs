using MediatR;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Interfaces;

using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Domain.Entities;

namespace PublishingTracker.Application.Books.Queries;

public record GetBookPerformanceQuery(int BookId) : IRequest<Result<List<BookPerformanceDto>>>;

public class GetBookPerformanceQueryHandler : IRequestHandler<GetBookPerformanceQuery, Result<List<BookPerformanceDto>>>
{
    private readonly IRepository<Book> _bookRepository;
    private readonly IRepository<Sale> _saleRepository;
    private readonly IQueryExecutor _queries;
    private readonly ICurrentUserService _currentUserService;

    public GetBookPerformanceQueryHandler(
        IRepository<Book> bookRepository, 
        IRepository<Sale> saleRepository, 
        IQueryExecutor queries,
        ICurrentUserService currentUserService)
    {
        _bookRepository = bookRepository;
        _saleRepository = saleRepository;
        _queries = queries;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<BookPerformanceDto>>> Handle(GetBookPerformanceQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Result<List<BookPerformanceDto>>.Failure("User is not authenticated.");

        var book = await _bookRepository.GetByIdAsync(request.BookId);
        if (book == null || book.UserId != userId)
        {
            return Result<List<BookPerformanceDto>>.Failure("Book not found or access denied.");
        }

        var performanceData = await _queries.ToListAsync(_saleRepository.Query()
            .Where(s => s.BookId == request.BookId)
            .GroupBy(s => new { s.Platform.Name, s.Currency })
            .Select(g => new BookPerformanceDto 
            { 
                PlatformName = g.Key.Name,
                Currency = g.Key.Currency,
                TotalRevenue = g.Sum(s => s.Revenue) 
            })
            , cancellationToken);

        return Result<List<BookPerformanceDto>>.Success(performanceData);
    }
}
