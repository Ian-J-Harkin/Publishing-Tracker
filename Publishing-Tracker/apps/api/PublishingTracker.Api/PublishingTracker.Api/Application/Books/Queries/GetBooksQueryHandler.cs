namespace PublishingTracker.Application.Books.Queries;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Common.Interfaces;
using MediatR;
using PublishingTracker.Api.Models; // For Result<T>

// 1. The DTO
public record BookResponseDto(
    int Id, 
    string Title, 
    string Author, 
    string? ISBN, 
    decimal? BasePrice,
    int TotalSold,
    decimal TotalRevenue    
);

// 2. The Query
public record GetBooksQuery(string? SearchTerm, string? SortBy, string? SortOrder, int Page, int PageSize) : IRequest<Result<List<BookResponseDto>>>;

// 3. The Handler
public class GetBooksQueryHandler : IRequestHandler<GetBooksQuery, Result<List<BookResponseDto>>>
{
    private readonly IBookRepository _bookRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetBooksQueryHandler(IBookRepository bookRepository, ICurrentUserService currentUserService)
    {
        _bookRepository = bookRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<BookResponseDto>>> Handle(GetBooksQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result<List<BookResponseDto>>.Failure("Unauthorized");

        var books = await _bookRepository.GetAllAsync(userId.Value, request.SearchTerm, request.SortBy, request.SortOrder, request.Page, request.PageSize, cancellationToken);

        return Result<List<BookResponseDto>>.Success(books);
    }
}
