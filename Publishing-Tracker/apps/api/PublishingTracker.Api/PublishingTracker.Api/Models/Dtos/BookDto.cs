using System;
using System.ComponentModel.DataAnnotations;

namespace PublishingTracker.Api.Models.Dtos
{
    public class BookDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string? ISBN { get; set; }
        public DateTime? PublicationDate { get; set; }
        public decimal? BasePrice { get; set; }
        public string? Genre { get; set; }
        public string? Description { get; set; }
    }

    public class CreateBookDto
    {
        [Required]
        [StringLength(255)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string Author { get; set; } = string.Empty;

        [StringLength(20)]
        public string? ISBN { get; set; }
        public DateTime? PublicationDate { get; set; }
        public decimal? BasePrice { get; set; }

        [StringLength(100)]
        public string? Genre { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateBookDto
    {
        [StringLength(255)]
        public string? Title { get; set; }

        [StringLength(255)]
        public string? Author { get; set; }

        [StringLength(20)]
        public string? ISBN { get; set; }
        public DateTime? PublicationDate { get; set; }
        public decimal? BasePrice { get; set; }

        [StringLength(100)]
        public string? Genre { get; set; }
        public string? Description { get; set; }
    }
    public class BookPerformanceDto
    {
        public string PlatformName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public string Currency { get; set; } = "USD";
    }
}