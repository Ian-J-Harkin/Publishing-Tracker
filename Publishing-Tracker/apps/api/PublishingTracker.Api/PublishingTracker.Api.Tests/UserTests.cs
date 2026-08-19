using System;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Domain.Exceptions;
using Xunit;

namespace PublishingTracker.Api.Tests
{
    public class UserTests
    {
        [Fact]
        public void Create_WithoutRole_DefaultsToUser()
        {
            var user = User.Create("test@test.com", "hash", "Test", "User");
            Assert.Equal("User", user.Role);
        }

        [Fact]
        public void Promote_ChangesRoleToAdmin_AndSetsRoleChangedAt()
        {
            var user = User.Create("test@test.com", "hash", "Test", "User");
            Assert.Null(user.RoleChangedAt);

            user.Promote();

            Assert.Equal("Admin", user.Role);
            Assert.NotNull(user.RoleChangedAt);
        }

        [Fact]
        public void Demote_WithMultipleAdmins_Succeeds()
        {
            var user = User.Create("test@test.com", "hash", "Test", "User", "Admin");
            
            // Assuming there are 2 admins in total, demoting this one should succeed.
            user.Demote(2);

            Assert.Equal("User", user.Role);
            Assert.NotNull(user.RoleChangedAt);
        }

        [Fact]
        public void Demote_WithOnlyOneAdmin_ThrowsException()
        {
            var user = User.Create("test@test.com", "hash", "Test", "User", "Admin");

            // Attempting to demote when they are the only admin (total count = 1)
            var ex = Assert.Throws<InvalidDomainStateException>(() => user.Demote(1));
            Assert.Contains("Cannot demote the last remaining Admin", ex.Message);
        }

        [Fact]
        public void UpdateProfile_WithValidData_UpdatesProperties()
        {
            var user = User.Create("test@test.com", "hash", "OldFirst", "OldLast");
            user.UpdateProfile("NewFirst", "NewLast");
            
            Assert.Equal("NewFirst", user.FirstName);
            Assert.Equal("NewLast", user.LastName);
        }

        [Fact]
        public void UpdateProfile_WithEmptyFirstName_ThrowsArgumentException()
        {
            var user = User.Create("test@test.com", "hash", "First", "Last");
            Assert.Throws<ArgumentException>(() => user.UpdateProfile("", "Last"));
        }

        [Fact]
        public void ChangePassword_WithValidHash_UpdatesPasswordHash()
        {
            var user = User.Create("test@test.com", "oldHash", "First", "Last");
            user.ChangePassword("newHash");
            
            Assert.Equal("newHash", user.PasswordHash);
        }

        [Fact]
        public void ChangePassword_WithEmptyHash_ThrowsArgumentException()
        {
            var user = User.Create("test@test.com", "oldHash", "First", "Last");
            Assert.Throws<ArgumentException>(() => user.ChangePassword(""));
        }
    }
}
