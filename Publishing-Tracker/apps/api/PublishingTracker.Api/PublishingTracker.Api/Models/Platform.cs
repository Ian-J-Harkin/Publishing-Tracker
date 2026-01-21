namespace PublishingTracker.Api.Models;

public class Platform
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public decimal? CommissionRate { get; set; }
    public string? ContactEmail { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public List<Sale> Sales { get; set; } = new();
}