using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Domain.Dtos;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Application.Books.Queries;

using PublishingTracker.Infrastructure.Persistence;

namespace PublishingTracker.Infrastructure.Persistence.Repositories;

/// <summary>
/// A generic repository implementation using EF Core.
/// Centralizes standard data access logic.
/// </summary>
public class BookRepository : IBookRepository
{
    private readonly PublishingTrackerDbContext _context;

    public BookRepository(PublishingTrackerDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Book book, CancellationToken cancellationToken)
    {
        await _context.Books.AddAsync(book, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<BookPerformanceDto>> GetBookPerformanceAsync(int bookId, CancellationToken cancellationToken)
    {
        // Infrastructure leakage is safely contained here!
        return await _context.Sales
            .Include(s => s.Platform)
            .Where(s => s.BookId == bookId)
            .GroupBy(s => new { s.Platform.Name, s.Currency })
            .Select(g => new BookPerformanceDto 
            { 
                PlatformName = g.Key.Name,
                Currency = g.Key.Currency,
                TotalRevenue = g.Sum(s => s.Revenue) 
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<BookResponseDto>> GetAllAsync(int userId, string? searchTerm, string? sortBy, string? sortOrder, int page, int pageSize, CancellationToken cancellationToken)
{
    var query = _context.Books.Where(b => b.UserId == userId);

    if (!string.IsNullOrWhiteSpace(searchTerm))
    {
        query = query.Where(b => b.Title.Contains(searchTerm) || b.Author.Contains(searchTerm));
    }

    // ... apply sorting and pagination logic here ...

    // IMPORTANT: We map directly to BookResponseDto here using .Select()
    // This is a major performance boost because we don't load the heavy 
    // Domain Entities into memory for Read-Only queries!
    return await query
        .Select(b => new BookResponseDto(
            b.Id,
            b.Title,
            b.Author,
            b.ISBN,
            b.BasePrice,
            b.Sales.Sum(s => s.Quantity),
            b.Sales.Sum(s => s.Revenue)
        ))
        .ToListAsync(cancellationToken);
}

       public async Task<Book?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        // Finds the book by its primary key
        return await _context.Books.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task UpdateAsync(Book book, CancellationToken cancellationToken)
    {
        // Marks the entity as modified and saves
        _context.Books.Update(book);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Book book, CancellationToken cancellationToken)
    {
        // Removes the entity and saves
        _context.Books.Remove(book);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Book> GetByIdAsync(object id, CancellationToken cancellationToken)
    {
        // Validate and convert the id parameter
        if (id is not int intId)
        {
            throw new ArgumentException("ID must be an integer", nameof(id));
        }
        
        // Reuse the existing GetByIdAsync method
        var book = await GetByIdAsync(intId, cancellationToken);
        
        // Handle null case - since this overload returns non-nullable Task<Book>,
        // we need to throw if the book is not found
        if (book == null)
        {
            throw new KeyNotFoundException($"Book with ID {intId} not found");
        }
        
        return book;
    }
}
