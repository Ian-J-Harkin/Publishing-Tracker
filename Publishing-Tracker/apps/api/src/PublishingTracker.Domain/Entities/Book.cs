using System;
using System.Collections.Generic;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Domain.Exceptions;

namespace PublishingTracker.Domain.Entities;

public class Book : IEntity
{
    public int Id { get; private set; }
    public int UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Author { get; private set; } = string.Empty;
    public string? SeriesName { get; private set; }
    public int? SeriesNumber { get; private set; }
    public string? Genre { get; private set; }
    public string? Description { get; private set; }
    public string? CoverImageUrl { get; private set; }
    
    [Auditable(isCreationTime: true)]
    public DateTime CreatedAt { get; private set; }

    [Auditable]
    public DateTime UpdatedAt { get; private set; }

    // Navigation properties
    public User User { get; private set; } = null!;
    public List<Edition> Editions { get; private set; } = new();
    public List<Sale> Sales { get; private set; } = new();

    // Private constructor for EF Core
    private Book() { }

    // Factory method enforcing invariants
    public Book(int userId, string title, string author, 
        string? seriesName = null, int? seriesNumber = null, 
        string? genre = null, string? description = null, 
        string? coverImageUrl = null)
    {
        if (string.IsNullOrWhiteSpace(title)) 
            throw new ArgumentException("Title is required", nameof(title));
        if (string.IsNullOrWhiteSpace(author)) 
            throw new ArgumentException("Author is required", nameof(author));

        UserId = userId;
        Title = title;
        Author = author;
        SeriesName = seriesName;
        SeriesNumber = seriesNumber;
        Genre = genre;
        Description = description;
        CoverImageUrl = coverImageUrl;
    }
    
    public void Update(string title, string author, 
        string? seriesName, int? seriesNumber, 
        string? genre, string? description, 
        string? coverImageUrl)
    {
        if (string.IsNullOrWhiteSpace(title)) 
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(author)) 
            throw new ArgumentException("Author cannot be empty.", nameof(author));
            
        Title = title;
        Author = author;
        SeriesName = seriesName;
        SeriesNumber = seriesNumber;
        Genre = genre;
        Description = description;
        CoverImageUrl = coverImageUrl;
    }

    public void VerifyOwnership(int userId)
    {
        if (UserId != userId) 
            throw new InvalidDomainStateException("User does not own this book.");
    }

    public Edition AddEdition(EditionFormat format, string? isbn = null, 
        DateTime? publicationDate = null, decimal? basePrice = null,
        int? pageCount = null, string? dimensions = null, decimal? fileSize = null,
        string? narrator = null, TimeSpan? duration = null)
    {
        var edition = new Edition(
            bookId: Id,
            format: format,
            isbn: isbn,
            publicationDate: publicationDate,
            basePrice: basePrice,
            pageCount: pageCount,
            dimensions: dimensions,
            fileSize: fileSize,
            narrator: narrator,
            duration: duration);
        Editions.Add(edition);
        return edition;
    }
}
