using System;
using System.Collections.Generic;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Domain.Exceptions;
namespace PublishingTracker.Domain.Entities
{
    public class Book : IEntity
    {
        public int Id { get; private set; }
        public int UserId { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Author { get; private set; } = string.Empty;
        public string? ISBN { get; private set; }
        public DateTime? PublicationDate { get; private set; }
        public decimal? BasePrice { get; private set; }
        public string? Genre { get; private set; }
        public string? Description { get; private set; }
        [Auditable(isCreationTime: true)]
        public DateTime CreatedAt { get; private set; }

        [Auditable]
        public DateTime UpdatedAt { get; private set; }

        // Navigation properties
        public User User { get; private set; } = null!;
        public List<Sale> Sales { get; private set; } = new();
        // Private constructor for EF Core
        private Book() { }

        // Factory method or public constructor enforcing invariants
        public Book(int userId, string title, string author, string? isbn = null, DateTime? publicationDate = null, decimal? basePrice = null, string? genre = null, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required");

            UserId = userId;
            Title = title;
            Author = author;
            ISBN = isbn;
            PublicationDate = publicationDate;
            BasePrice = basePrice;
            Genre = genre;
            Description = description;
        }
        
        public void Update(string title, string author, string? isbn, DateTime? publicationDate = null, decimal? basePrice = null, string? genre = null, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title cannot be empty.");
            Title = title;
            Author = author;
            ISBN = isbn;
            PublicationDate = publicationDate;
            BasePrice = basePrice;
            Genre = genre;
            Description = description;
        }
        // Domain behavior
        public void UpdatePricing(decimal newPrice)
        {
            if (newPrice < 0) throw new InvalidDomainStateException("Price cannot be negative.");
            BasePrice = newPrice;
        }

        public void VerifyOwnership(int userId)
        {
            if (UserId != userId) throw new InvalidDomainStateException("User does not own this book.");
        }
    }
}
