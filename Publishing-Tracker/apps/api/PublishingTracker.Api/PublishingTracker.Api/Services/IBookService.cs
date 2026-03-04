using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;

namespace PublishingTracker.Api.Services;

public interface IBookService
{
    Task<Result<List<Book>>> GetAllAsync(string? searchTerm = null);
    Task<Result<Book>> GetByIdAsync(int id);
    Task<Result<Book>> CreateAsync(CreateBookDto dto);
    Task<Result<Book>> UpdateAsync(int id, UpdateBookDto dto);
    Task<Result> DeleteAsync(int id);
    Task<Result<List<BookPerformanceDto>>> GetPerformanceAsync(int bookId);
}
