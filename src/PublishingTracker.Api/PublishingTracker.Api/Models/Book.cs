using System;
using System.Collections.Generic;

namespace PublishingTracker.Api.Models
{
    public class Book
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string? ISBN { get; set; }
        public DateTime? PublicationDate { get; set; }
        public decimal? BasePrice { get; set; }
        public string? Genre { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        public User User { get; set; } = null!;
        public List<Sale> Sales { get; set; } = new();
    }
}