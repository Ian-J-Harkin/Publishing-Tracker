namespace PublishingTracker.Application.Common.Dtos
{
    public class CurrencyTotalDto
    {
        public string Currency { get; set; } = "USD";
        public decimal TotalAmount { get; set; }
    }
}
