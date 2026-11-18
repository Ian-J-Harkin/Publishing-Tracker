using System;
using PublishingTracker.Domain;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Domain.Constants;

namespace PublishingTracker.Domain.Entities
{
    public class Sale : IEntity
    {
        public int Id { get; private set; }
        public int BookId { get; private set; }
        public int? EditionId { get; private set; }
        public int PlatformId { get; private set; }
        public DateTime SaleDate { get; private set; }
        public int Quantity { get; private set; }
        public decimal UnitPrice { get; private set; }
        public decimal Royalty { get; private set; }
        public decimal Revenue { get; private set; }
        public string Currency { get; private set; } = CurrencyConstants.DefaultCurrency;
        public decimal PlatformCommissionRate { get; private set; }
        public decimal ExchangeRate { get; private set; }
        public decimal BaseCurrencyRevenue { get; private set; }
        public decimal BaseCurrencyRoyalty { get; private set; }
        public string? OrderId { get; private set; }
        
        [Auditable(isCreationTime: true)]
        public DateTime CreatedAt { get; private set; }

        // Navigation properties
        public Book Book { get; private set; } = null!;
        public Edition? Edition { get; private set; }
        public Platform Platform { get; private set; } = null!;

        private Sale() { }

        public static Sale Create(
            int bookId, 
            int platformId, 
            DateTime saleDate, 
            int quantity, 
            decimal unitPrice, 
            decimal royalty, 
            decimal platformCommissionRate, 
            decimal exchangeRate = CurrencyConstants.NoConversionRate, 
            string? currency = null, 
            string? orderId = null, 
            decimal? revenue = null, 
            int? editionId = null)
        {
            if (quantity <= 0) 
                throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
            if (unitPrice < 0) 
                throw new ArgumentException("UnitPrice cannot be negative.", nameof(unitPrice));
            if (royalty < 0) 
                throw new ArgumentException("Royalty cannot be negative.", nameof(royalty));
            if (platformCommissionRate < 0) 
                throw new ArgumentException("Commission rate cannot be negative.", nameof(platformCommissionRate));
            if (exchangeRate <= 0) 
                throw new ArgumentException("Exchange rate must be greater than zero.", nameof(exchangeRate));

            var calculatedRevenue = revenue ?? (royalty > 0 ? quantity * royalty : quantity * unitPrice);
            var baseRevenue = calculatedRevenue * exchangeRate;
            var baseRoyalty = royalty * exchangeRate;

            var normalizedSaleDate = saleDate.Kind switch
            {
                DateTimeKind.Utc => saleDate,
                DateTimeKind.Local => saleDate.ToUniversalTime(),
                _ => DateTime.SpecifyKind(saleDate, DateTimeKind.Utc)
            };

            return new Sale
            {
                BookId = bookId,
                EditionId = editionId,
                PlatformId = platformId,
                SaleDate = normalizedSaleDate,
                Quantity = quantity,
                UnitPrice = unitPrice,
                Royalty = royalty,
                Revenue = calculatedRevenue,
                Currency = currency ?? CurrencyConstants.DefaultCurrency,
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
