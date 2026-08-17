using System;
using PublishingTracker.Api.Domain.Entities;
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
    }
}
