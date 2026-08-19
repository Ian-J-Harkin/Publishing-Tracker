namespace PublishingTracker.Application.Books.Commands;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Common.Interfaces;
using MediatR;
 // For Result<T>

public record DeleteBookCommand(int Id) : IRequest<Result<bool>>;

public class DeleteBookCommandHandler : IRequestHandler<DeleteBookCommand, Result<bool>>
{
    private readonly IBookRepository _bookRepository;
    private readonly ICurrentUserService _currentUserService;

    public DeleteBookCommandHandler(IBookRepository bookRepository, ICurrentUserService currentUserService)
    {
        _bookRepository = bookRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(DeleteBookCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result<bool>.Failure("Unauthorized");

        // 1. Get the book to delete
        var book = await _bookRepository.GetByIdAsync(request.Id, cancellationToken);
        if (book == null) return Result<bool>.Failure("Book not found");

        // Verify ownership
        book.VerifyOwnership(userId.Value);

        // 2. Delete using the Port interface (Dependency Inversion)
        await _bookRepository.DeleteAsync(book, cancellationToken);

        return Result<bool>.Success(true);
    }
}
    
