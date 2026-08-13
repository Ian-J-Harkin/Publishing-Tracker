using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Api.Models;
using PublishingTracker.Domain.Dtos;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Domain;

using PublishingTracker.Domain.Entities;

namespace PublishingTracker.Api.Services;

public class BookService : IBookService
{
    private readonly IRepository<Book> _bookRepository;
    private readonly IRepository<Sale> _saleRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<BookService> _logger;

    public BookService(
        IRepository<Book> bookRepository,
        IRepository<Sale> saleRepository,
        ICurrentUserService currentUserService,
        ILogger<BookService> logger)
    {
        _bookRepository = bookRepository;
        _saleRepository = saleRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<List<Book>>> GetAllAsync(string? searchTerm = null)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Result<List<Book>>.Failure("User is not authenticated.");

        _logger.LogInformation("Fetching books for user {UserId}.", userId);

        var books = await _bookRepository.Query()
            .ForUser(userId.Value)
            .Search(searchTerm)
            .OrderByDescending(b => b.PublicationDate)
            .ThenBy(b => b.Title)
            .ToListAsync();

        return Result<List<Book>>.Success(books);
    }

    public async Task<Result<Book>> GetByIdAsync(int id)
    {
        _logger.LogInformation("Fetching book with ID {BookId}.", id);
        var book = await _bookRepository.GetByIdAsync(id);

        if (book == null)
            return Result<Book>.Failure($"Book with ID {id} not found.");

        // Implicit authorization check
        if (book.UserId != _currentUserService.UserId)
            return Result<Book>.Failure("Access denied.");

        return Result<Book>.Success(book);
    }

    //public async Task<Result<Book>> CreateAsync(CreateBookDto dto)
    //{
    //    var userId = _currentUserService.UserId;
    //    if (userId == null)
    //        return Result<Book>.Failure("User is not authenticated.");

    //    var book = new Book
    //    {
    //        Title = dto.Title,
    //        Author = dto.Author,
    //        ISBN = dto.ISBN,
    //        PublicationDate = dto.PublicationDate,
    //        BasePrice = dto.BasePrice,
    //        Genre = dto.Genre,
    //        Description = dto.Description,
    //        UserId = userId.Value
    //        // CreatedAt and UpdatedAt are now handled automatically by the DbContext via [Auditable] attributes!
    //    };

    //    var newBook = await _bookRepository.AddAsync(book);
    //    _logger.LogInformation("User {UserId} created a new book (BookId: {BookId}).", userId, newBook.Id);

    //    return Result<Book>.Success(newBook);
    //}

    //public async Task<Result<Book>> UpdateAsync(int id, UpdateBookDto dto)
    //{
    //    var book = await _bookRepository.GetByIdAsync(id);
    //    if (book == null)
    //    {
    //        _logger.LogWarning("Book with ID {BookId} not found for update.", id);
    //        return Result<Book>.Failure($"Book with ID {id} not found.");
    //    }

    //    if (book.UserId != _currentUserService.UserId)
    //        return Result<Book>.Failure("Access denied.");

    //    if (dto.Title != null) book.Title = dto.Title;
    //    if (dto.Author != null) book.Author = dto.Author;
    //    if (dto.ISBN != null) book.ISBN = dto.ISBN;
    //    if (dto.PublicationDate.HasValue) book.PublicationDate = dto.PublicationDate;
    //    if (dto.BasePrice.HasValue) book.BasePrice = dto.BasePrice;
    //    if (dto.Genre != null) book.Genre = dto.Genre;
    //    if (dto.Description != null) book.Description = dto.Description;

    //    await _bookRepository.UpdateAsync(book);
    //    _logger.LogInformation("Book with ID {BookId} was updated.", id);

    //    return Result<Book>.Success(book);
    //}

    public async Task<Result> DeleteAsync(int id)
    {
        var book = await _bookRepository.GetByIdAsync(id);
        if (book == null)
        {
            _logger.LogWarning("Book with ID {BookId} not found for deletion.", id);
            return Result.Failure($"Book with ID {id} not found.");
        }

        if (book.UserId != _currentUserService.UserId)
            return Result.Failure("Access denied.");

        await _bookRepository.DeleteAsync(book);
        _logger.LogInformation("Book with ID {BookId} was deleted.", id);

        return Result.Success();
    }

    public async Task<Result<List<BookPerformanceDto>>> GetPerformanceAsync(int bookId)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Result<List<BookPerformanceDto>>.Failure("User is not authenticated.");

        var book = await _bookRepository.GetByIdAsync(bookId);
        if (book == null || book.UserId != userId)
        {
            _logger.LogWarning("User {UserId} tried to access performance data for a book they do not own (BookId: {BookId}).", userId, bookId);
            return Result<List<BookPerformanceDto>>.Failure("Book not found or access denied.");
        }

        _logger.LogInformation("Fetching performance data for BookId {BookId} for User {UserId}.", bookId, userId);
        
        var performanceData = await _saleRepository.Query()
            .Include(s => s.Platform)
            .Where(s => s.BookId == bookId)
            .GroupBy(s => new { s.Platform.Name, s.Currency })
            .Select(g => new BookPerformanceDto 
            { 
                PlatformName = g.Key.Name,
                Currency = g.Key.Currency,
                TotalRevenue = g.Sum(s => s.Revenue) 
            })
            .ToListAsync();

        return Result<List<BookPerformanceDto>>.Success(performanceData);
    }
}
