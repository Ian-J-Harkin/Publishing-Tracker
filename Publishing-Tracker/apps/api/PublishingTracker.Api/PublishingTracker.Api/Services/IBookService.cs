using PublishingTracker.Api.Models;
using PublishingTracker.Domain.Dtos;
using PublishingTracker.Domain.Entities;

namespace PublishingTracker.Api.Services;

public interface IBookService
{
    // Task<Result<List<Book>>> GetAllAsync(string? searchTerm = null);
    Task<Result<Book>> GetByIdAsync(int id);
    //Task<Result<Book>> CreateAsync(CreateBookDto dto);
  //  Task<Result<Book>> UpdateAsync(int id, UpdateBookDto dto);
    Task<Result> DeleteAsync(int id);
    Task<Result<List<BookPerformanceDto>>> GetPerformanceAsync(int bookId);
}
