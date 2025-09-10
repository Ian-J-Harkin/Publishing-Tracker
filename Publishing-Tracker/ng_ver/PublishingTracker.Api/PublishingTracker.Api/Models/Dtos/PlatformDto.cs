namespace PublishingTracker.Api.Models.Dtos
{
    public class PlatformDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string BaseUrl { get; set; }
        public decimal CommissionRate { get; set; }
    }

    public class PlatformRequestDto
    {
        public string Name { get; set; }
        public string BaseUrl { get; set; }
        public decimal CommissionRate { get; set; }
    }
}