

namespace PublishingTracker.Application.Books.Commands;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Common.Interfaces;
using MediatR;
using PublishingTracker.Api.Models; // For Result<T>

public record CreateBookCommand(string Title, string Author, string ISBN) : IRequest<Result<int>>;

public class CreateBookCommandHandler : IRequestHandler<CreateBookCommand, Result<int>>
{
    private readonly IBookRepository _bookRepository;
    private readonly ICurrentUserService _currentUserService;

    public CreateBookCommandHandler(IBookRepository bookRepository, ICurrentUserService currentUserService)
    {
        _bookRepository = bookRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<int>> Handle(CreateBookCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result<int>.Failure("Unauthorized");

        // 1. Create using Domain logic
        var book = new Book(userId.Value, request.Title, request.Author);
        
        // Optional: add other properties via domain methods
        // book.SetIsbn(request.ISBN);

        // 2. Persist using the Port interface (Dependency Inversion)
        await _bookRepository.AddAsync(book, cancellationToken);

        return Result<int>.Success(book.Id);
    }
}
