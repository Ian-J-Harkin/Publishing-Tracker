using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;

namespace PublishingTracker.Api.Services;

public interface IBookService
{
    Task<List<Book>> GetAllAsync(string? searchTerm = null);
    Task<Book?> GetByIdAsync(int id);
    Task<Book> CreateAsync(CreateBookDto dto);
    Task<Book?> UpdateAsync(int id, UpdateBookDto dto);
    Task DeleteAsync(int id);
    Task<List<BookPerformanceDto>?> GetPerformanceAsync(int bookId);
}
