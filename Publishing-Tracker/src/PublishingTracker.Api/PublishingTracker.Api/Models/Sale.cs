using System;

namespace PublishingTracker.Api.Models
{
    public class Sale
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public int PlatformId { get; set; }
        public DateTime SaleDate { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Royalty { get; set; }
        public decimal Revenue { get; set; }
        public string Currency { get; set; } = "USD";
        public string? OrderId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public Book Book { get; set; } = null!;
        public Platform Platform { get; set; } = null!;
    }
}