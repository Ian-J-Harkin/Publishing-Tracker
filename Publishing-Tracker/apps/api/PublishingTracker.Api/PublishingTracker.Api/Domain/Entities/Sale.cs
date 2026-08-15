using System;
using PublishingTracker.Domain;
using PublishingTracker.Domain.Entities;
namespace  PublishingTracker.Api.Domain.Entities
{
    public class Sale : IEntity
    {
        public int Id { get; private set; }
        public int BookId { get; private set; }
        public int PlatformId { get; private set; }
        public DateTime SaleDate { get; private set; }
        public int Quantity { get; private set; }
        public decimal UnitPrice { get; private set; }
        public decimal Royalty { get; private set; }
        public decimal Revenue { get; private set; }
        public string Currency { get; private set; } = "USD";
        public string? OrderId { get; private set; }
        [Auditable(isCreationTime: true)]
        public DateTime CreatedAt { get; private set; }

        // Navigation properties
        public Book Book { get; private set; } = null!;
        public Platform Platform { get; private set; } = null!;

        private Sale() { }

        public static Sale Create(int bookId, int platformId, DateTime saleDate, int quantity, decimal unitPrice, decimal royalty, string currency = "USD", string? orderId = null, decimal? revenue = null)
        {
            if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
            if (unitPrice < 0) throw new ArgumentException("UnitPrice cannot be negative.", nameof(unitPrice));
            if (royalty < 0) throw new ArgumentException("Royalty cannot be negative.", nameof(royalty));

            return new Sale
            {
                BookId = bookId,
                PlatformId = platformId,
                SaleDate = saleDate,
                Quantity = quantity,
                UnitPrice = unitPrice,
                Royalty = royalty,
                Revenue = revenue ?? (royalty > 0 ? quantity * royalty : quantity * unitPrice),
                Currency = currency,
                OrderId = orderId,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}