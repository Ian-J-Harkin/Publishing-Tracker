using System.ComponentModel.DataAnnotations;
using PublishingTracker.Domain;
using PublishingTracker.Domain.Exceptions;
namespace PublishingTracker.Domain.Entities
{
    public class User : IEntity
    {
        public int Id { get; private set; }

        [Required]
        [EmailAddress]
        public string Email { get; private set; } = string.Empty;

        [Required]
        public string PasswordHash { get; private set; } = string.Empty;

        [Required]
        public string FirstName { get; private set; } = string.Empty;

        [Required]
        public string LastName { get; private set; } = string.Empty;

        [Required]
        public string Role { get; private set; } = "User";

        public DateTime? RoleChangedAt { get; private set; }

        [Auditable(isCreationTime: true)]
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

        [Auditable]
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        // Factory / invariant enforcement
        public static User Create(string email, string passwordHash, string firstName, string lastName, string role = "User")
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email required", nameof(email));
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("PasswordHash required", nameof(passwordHash));
            if (role != "User" && role != "Admin")
                throw new ArgumentException("Role must be 'User' or 'Admin'.", nameof(role));
            return new User { Email = email, PasswordHash = passwordHash, FirstName = firstName, LastName = lastName, Role = role, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        }

        /// <summary>
        /// Promotes this user to Admin.
        /// </summary>
        public void Promote()
        {
            if (Role == "Admin")
                throw new InvalidDomainStateException("User is already an Admin.");
            Role = "Admin";
            RoleChangedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Demotes this user from Admin to User.
        /// The caller must supply the current total number of admins in the system.
        /// The domain enforces that the last admin cannot be demoted.
        /// </summary>
        public void Demote(int totalAdminCount)
        {
            if (Role != "Admin")
                throw new InvalidDomainStateException("User is not an Admin.");
            if (totalAdminCount <= 1)
                throw new InvalidDomainStateException("Cannot demote the last remaining Admin. Promote another user first.");
            Role = "User";
            RoleChangedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Updates the user's profile details.
        /// </summary>
        public void UpdateProfile(string firstName, string lastName)
        {
            if (string.IsNullOrWhiteSpace(firstName))
                throw new ArgumentException("First name cannot be empty.", nameof(firstName));
            if (string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("Last name cannot be empty.", nameof(lastName));
                
            FirstName = firstName;
            LastName = lastName;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Updates the user's email address.
        /// </summary>
        public void UpdateEmail(string newEmail)
        {
            if (string.IsNullOrWhiteSpace(newEmail))
                throw new ArgumentException("Email cannot be empty.", nameof(newEmail));

            Email = newEmail;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Changes the user's password.
        /// </summary>
        public void ChangePassword(string newPasswordHash)
        {
            if (string.IsNullOrWhiteSpace(newPasswordHash))
                throw new ArgumentException("Password hash cannot be empty.", nameof(newPasswordHash));
                
            PasswordHash = newPasswordHash;
            UpdatedAt = DateTime.UtcNow;
        }

        // Private ctor for EF Core
        private User() { }
    }
}
