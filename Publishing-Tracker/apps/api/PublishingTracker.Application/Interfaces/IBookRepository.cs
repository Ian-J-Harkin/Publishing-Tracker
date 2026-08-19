namespace PublishingTracker.Application.Interfaces;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Domain.Dtos;

using PublishingTracker.Application.Books.Queries;

public interface IBookRepository
{
    Task<Book?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task AddAsync(Book book, CancellationToken cancellationToken);
    Task UpdateAsync(Book book, CancellationToken cancellationToken);
    Task DeleteAsync(Book book, CancellationToken cancellationToken);
    
    // Read-only queries can return specialized DTOs directly to bypass the domain model for performance
    Task<List<BookPerformanceDto>> GetBookPerformanceAsync(int bookId, CancellationToken cancellationToken);
    
    Task<List<BookResponseDto>> GetAllAsync(int userId, string? searchTerm, string? sortBy, string? sortOrder, int page, int pageSize, CancellationToken cancellationToken);
    Task<Book> GetByIdAsync(object id, CancellationToken cancellationToken);
}