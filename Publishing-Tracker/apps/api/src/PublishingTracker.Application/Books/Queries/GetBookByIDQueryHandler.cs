namespace PublishingTracker.Application.Books.Queries;
using MediatR;
 // Result<T>
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Application.Common.Interfaces;
using System.Linq;

// Query
public record GetBookByIdQuery(int Id) : IRequest<Result<BookResponseDto>>;

// Handler
public class GetBookByIDQueryHandler : IRequestHandler<GetBookByIdQuery, Result<BookResponseDto>>
{
    private readonly IBookRepository _bookRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetBookByIDQueryHandler(IBookRepository bookRepository, ICurrentUserService currentUserService)
    {
        _bookRepository = bookRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<BookResponseDto>> Handle(GetBookByIdQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result<BookResponseDto>.Failure("Unauthorized");

        var book = await _bookRepository.GetByIdAsync(request.Id, cancellationToken);
        if (book == null) return Result<BookResponseDto>.Failure($"Book with ID {request.Id} not found.");

        if (book.UserId != userId.Value) return Result<BookResponseDto>.Failure("Access denied.");

        var totalSold = book.Sales?.Sum(s => s.Quantity) ?? 0;
        var totalRevenue = book.Sales?.Sum(s => s.BaseCurrencyRevenue) ?? 0m;

        var dto = new BookResponseDto
        {
            Id = book.Id,
            Title = book.Title,
            Author = book.Author,
            ISBN = book.ISBN,
            PublicationDate = book.PublicationDate,
            BasePrice = book.BasePrice,
            Genre = book.Genre,
            Description = book.Description,
            TotalSales = totalSold,
            TotalRevenue = totalRevenue
        };

        return Result<BookResponseDto>.Success(dto);
    }
}
