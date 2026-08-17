namespace PublishingTracker.Application.Books.Commands;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Common.Interfaces;  
using MediatR;
using PublishingTracker.Api.Models; // For Result<T>
    
public record UpdateBookCommand(int Id, string Title, string Author, string? ISBN, DateTime? PublicationDate, decimal? BasePrice, string? Genre, string? Description) : IRequest<Result<int>>;

public class UpdateBookCommandHandler : IRequestHandler<UpdateBookCommand, Result<int>>
{
    private readonly IBookRepository _bookRepository;
    private readonly ICurrentUserService _currentUserService;

    public UpdateBookCommandHandler(IBookRepository bookRepository, ICurrentUserService currentUserService)
    {
        _bookRepository = bookRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<int>> Handle(UpdateBookCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result<int>.Failure("Unauthorized");

        // 1. Get the book to update
        var book = await _bookRepository.GetByIdAsync(request.Id, cancellationToken);
        if (book == null) return Result<int>.Failure("Book not found");

        // Verify ownership
        book.VerifyOwnership(userId.Value);

        // Update using Domain logic
        book.Update(request.Title, request.Author, request.ISBN, request.PublicationDate, request.BasePrice, request.Genre, request.Description);
        
        // Optional: add other properties via domain methods
        // book.SetIsbn(request.ISBN);

        // 2. Persist using the Port interface (Dependency Inversion)
        await _bookRepository.UpdateAsync(book, cancellationToken);

        return Result<int>.Success(book.Id);
    }
}
