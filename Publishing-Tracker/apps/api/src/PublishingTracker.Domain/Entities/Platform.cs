namespace PublishingTracker.Domain.Entities;

using PublishingTracker.Domain.Entities;
using PublishingTracker.Domain;
using PublishingTracker.Domain.Exceptions;

public class Platform : IEntity
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? BaseUrl { get; private set; }
    public decimal CommissionRate { get; private set; }
    public string? ContactEmail { get; private set; }
    public bool IsActive { get; private set; } = false;
    public int? CreatedByUserId { get; private set; }
    
    [Auditable(isCreationTime: true)]
    public DateTime CreatedAt { get; private set; }
    
    public List<Sale> Sales { get; private set; } = new();

    private Platform() { }

    public static Platform Create(string name, string? baseUrl, decimal commissionRate, string? contactEmail = null, int? createdByUserId = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Platform name is required.");
        if (commissionRate < 0) throw new ArgumentException("Commission rate cannot be negative.");

        return new Platform
        {
            Name = name,
            BaseUrl = baseUrl,
            CommissionRate = commissionRate,
            ContactEmail = contactEmail,
            IsActive = false,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Approves this platform, making it active and available to all users.
    /// </summary>
    public void Approve()
    {
        if (IsActive)
            throw new InvalidDomainStateException("Platform is already active.");
        IsActive = true;
    }
}
