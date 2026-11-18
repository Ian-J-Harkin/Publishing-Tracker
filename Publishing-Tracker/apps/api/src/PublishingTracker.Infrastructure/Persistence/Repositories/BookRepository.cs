using Microsoft.EntityFrameworkCore;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Application.Books.Queries;

namespace PublishingTracker.Infrastructure.Persistence.Repositories;

public class BookRepository : IBookRepository
{
    private readonly PublishingTrackerDbContext _context;

    public BookRepository(PublishingTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<Book?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Books
            .Include(b => b.User)
            .Include(b => b.Editions)
                .ThenInclude(e => e.PlatformMetadata)
            .Include(b => b.Sales)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<Book> GetByIdAsync(object id, CancellationToken cancellationToken)
    {
        if (id is int intId)
        {
            var book = await GetByIdAsync(intId, cancellationToken);
            return book ?? throw new Exception("Book not found");
        }
        throw new ArgumentException("Id must be an integer", nameof(id));
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
                TotalRevenue = g.Sum(s => s.BaseCurrencyRevenue) 
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<BookResponseDto>> GetAllAsync(int userId, string? searchTerm, string? sortBy, string? sortOrder, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.Books
            .Include(b => b.Editions)
            .Include(b => b.Sales)
            .Where(b => b.UserId == userId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(b => b.Title.Contains(searchTerm) || b.Author.Contains(searchTerm));
        }

        query = sortBy?.ToLower() switch
        {
            "title" => sortOrder?.ToLower() == "desc" ? query.OrderByDescending(b => b.Title) : query.OrderBy(b => b.Title),
            "author" => sortOrder?.ToLower() == "desc" ? query.OrderByDescending(b => b.Author) : query.OrderBy(b => b.Author),
            "publicationdate" => sortOrder?.ToLower() == "desc" 
                ? query.OrderByDescending(b => b.Editions.Min(e => e.PublicationDate)) 
                : query.OrderBy(b => b.Editions.Min(e => e.PublicationDate)),
            _ => query.OrderByDescending(b => b.CreatedAt)
        };

        var skip = (page - 1) * pageSize;

        return await query
            .Skip(skip)
            .Take(pageSize)
            .Select(b => new BookResponseDto
            {
                Id = b.Id,
                Title = b.Title,
                Author = b.Author,
                ISBN = b.Editions.OrderBy(e => e.Id).Select(e => e.ISBN).FirstOrDefault(),
                PublicationDate = b.Editions.Min(e => e.PublicationDate),
                BasePrice = b.Editions.OrderBy(e => e.Id).Select(e => e.BasePrice).FirstOrDefault(),
                Genre = b.Genre,
                Description = b.Description,
                TotalSales = b.Sales.Count,
                TotalRevenue = b.Sales.Sum(s => s.BaseCurrencyRevenue)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Book?> GetByIdWithSalesAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Books
            .Include(b => b.Sales)
            .ThenInclude(s => s.Platform)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Book book, CancellationToken cancellationToken)
    {
        _context.Books.Update(book);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Book book, CancellationToken cancellationToken)
    {
        _context.Books.Remove(book);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
