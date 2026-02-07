namespace PublishingTracker.Api.Models.Dtos
{
    public class CurrencyTotalDto
    {
        public string Currency { get; set; } = "USD";
        public decimal TotalAmount { get; set; }
    }
}
