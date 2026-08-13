using System.ComponentModel.DataAnnotations;
using PublishingTracker.Domain;
namespace PublishingTracker.Api.Domain.Entities
{
    public class User : IEntity
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; private set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Auditable(isCreationTime: true)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Auditable]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // // Factory / invariant enforcement
        public static User Create(string email, string passwordHash, string firstName, string lastName)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email required", nameof(email));
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("PasswordHash required", nameof(passwordHash));
            return new User { Email = email, PasswordHash = passwordHash, FirstName = firstName, LastName = lastName, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        }

        // Private ctor for EF Core
        private User() { }
    }
}