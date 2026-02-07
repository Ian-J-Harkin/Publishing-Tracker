                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            using System;
using System.ComponentModel.DataAnnotations;

namespace PublishingTracker.Api.Models.Dtos
{
    public class SaleDto
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public int PlatformId { get; set; }
        public string PlatformName { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Royalty { get; set; }
        public decimal Revenue { get; set; }
    }

    public class CreateSaleDto
    {
        [Required]
        public int BookId { get; set; }

        [Required]
        public int PlatformId { get; set; }

        [Required]
        public DateTime SaleDate { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Royalty { get; set; }

        public string Currency { get; set; } = "USD";
    }
}