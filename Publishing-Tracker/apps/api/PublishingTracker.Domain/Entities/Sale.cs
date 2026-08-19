using System;
using PublishingTracker.Domain;
using PublishingTracker.Domain.Entities;
namespace  PublishingTracker.Domain.Entities
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
        public decimal PlatformCommissionRate { get; private set; }
        public decimal ExchangeRate { get; private set; }
        public decimal BaseCurrencyRevenue { get; private set; }
        public decimal BaseCurrencyRoyalty { get; private set; }
        public string? OrderId { get; private set; }
        [Auditable(isCreationTime: true)]
        public DateTime CreatedAt { get; private set; }

        // Navigation properties
        public Book Book { get; private set; } = null!;
        public Platform Platform { get; private set; } = null!;

        private Sale() { }

        public static Sale Create(int bookId, int platformId, DateTime saleDate, int quantity, decimal unitPrice, decimal royalty, decimal platformCommissionRate, decimal exchangeRate = 1.0m, string currency = "USD", string? orderId = null, decimal? revenue = null)
        {
            if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
            if (unitPrice < 0) throw new ArgumentException("UnitPrice cannot be negative.", nameof(unitPrice));
            if (royalty < 0) throw new ArgumentException("Royalty cannot be negative.", nameof(royalty));
            if (platformCommissionRate < 0) throw new ArgumentException("Commission rate cannot be negative.", nameof(platformCommissionRate));
            if (exchangeRate <= 0) throw new ArgumentException("Exchange rate must be greater than zero.", nameof(exchangeRate));

            var calculatedRevenue = revenue ?? (royalty > 0 ? quantity * royalty : quantity * unitPrice);
            var baseRevenue = calculatedRevenue * exchangeRate;
            var baseRoyalty = royalty * exchangeRate;

            return new Sale
            {
                BookId = bookId,
                PlatformId = platformId,
                SaleDate = saleDate,
                Quantity = quantity,
                UnitPrice = unitPrice,
                Royalty = royalty,
                Revenue = calculatedRevenue,
                Currency = currency,
                PlatformCommissionRate = platformCommissionRate,
                ExchangeRate = exchangeRate,
                BaseCurrencyRevenue = baseRevenue,
                BaseCurrencyRoyalty = baseRoyalty,
                OrderId = orderId,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
