using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;

namespace PublishingTracker.Api.Services;

public class BookService : IBookService
{
    private readonly PublishingTrackerDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<BookService> _logger;

    public BookService(
        PublishingTrackerDbContext context,
        ICurrentUserService currentUserService,
        ILogger<BookService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<List<Book>> GetAllAsync()
    {
        _logger.LogInformation("Fetching all books.");
        return await _context.Books.ToListAsync();
    }

    public async Task<Book?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Fetching book with ID {BookId}.", id);
        return await _context.Books.FindAsync(id);
    }

    public async Task<Book> CreateAsync(CreateBookDto dto)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var book = new Book
        {
            Title = dto.Title,
            Author = dto.Author,
            ISBN = dto.ISBN,
            PublicationDate = dto.PublicationDate,
            BasePrice = dto.BasePrice,
            Genre = dto.Genre,
            Description = dto.Description,
            UserId = userId.Value,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Books.Add(book);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} created a new book (BookId: {BookId}).", userId, book.Id);
        return book;
    }

    public async Task<Book?> UpdateAsync(int id, UpdateBookDto dto)
    {
        var book = await _context.Books.FindAsync(id);
        if (book == null)
        {
            _logger.LogWarning("Book with ID {BookId} not found for update.", id);
            return null;
        }

        if (dto.Title != null) book.Title = dto.Title;
        if (dto.Author != null) book.Author = dto.Author;
        if (dto.ISBN != null) book.ISBN = dto.ISBN;
        if (dto.PublicationDate.HasValue) book.PublicationDate = dto.PublicationDate;
        if (dto.BasePrice.HasValue) book.BasePrice = dto.BasePrice;
        if (dto.Genre != null) book.Genre = dto.Genre;
        if (dto.Description != null) book.Description = dto.Description;
        book.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Book with ID {BookId} was updated.", id);
        return book;
    }

    public async Task DeleteAsync(int id)
    {
        var book = await _context.Books.FindAsync(id);
        if (book == null)
        {
            _logger.LogWarning("Book with ID {BookId} not found for deletion.", id);
            return;
        }

        _context.Books.Remove(book);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Book with ID {BookId} was deleted.", id);
    }

    public async Task<List<BookPerformanceDto>?> GetPerformanceAsync(int bookId)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
        {
            return null; // Implicit authorization failure
        }

        var book = await _context.Books.FirstOrDefaultAsync(b => b.Id == bookId && b.UserId == userId);
        if (book == null)
        {
            _logger.LogWarning("User {UserId} tried to access performance data for a book they do not own (BookId: {BookId}).", userId, bookId);
            return null; // Not found or not owned
        }

        _logger.LogInformation("Fetching performance data for BookId {BookId} for User {UserId}.", bookId, userId);
        var performanceData = await _context.Sales
            .Where(s => s.BookId == bookId)
            .GroupBy(s => s.Platform.Name)
            .Select(g => new BookPerformanceDto 
            { 
                PlatformName = g.Key, 
                TotalRevenue = g.Sum(s => s.Revenue) 
            })
            .ToListAsync();

        return performanceData;
    }
}
