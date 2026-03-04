using System.ComponentModel.DataAnnotations;

namespace PublishingTracker.Api.Models
{
    public class User : IEntity
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Auditable(isCreationTime: true)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Auditable]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}