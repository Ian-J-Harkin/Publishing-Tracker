namespace PublishingTracker.Api.Models.Dtos
{
    public class SalesSummaryDto
    {
        public decimal TotalRevenue { get; set; }
        public int TotalUnitsSold { get; set; }
        public decimal AverageRoyalty { get; set; }
        public int SalesCount { get; set; }
    }
}
