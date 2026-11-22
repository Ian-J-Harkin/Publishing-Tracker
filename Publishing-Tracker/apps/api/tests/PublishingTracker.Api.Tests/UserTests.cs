using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Users.Commands;
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

        [Fact]
        public async System.Threading.Tasks.Task PromoteUserCommandHandler_WhenAlreadyAdmin_ReturnsFailureInsteadOfThrowing()
        {
            var admin = User.Create("admin@test.com", "hash", "Admin", "User", "Admin");
            var context = new Moq.Mock<PublishingTracker.Application.Common.Interfaces.IApplicationDbContext>();
            context.SetupGet(c => c.Users).Returns(new[] { admin }.AsQueryable());

            var queries = new Mock<IQueryExecutor>();
            queries.Setup(q => q.FirstOrDefaultAsync(It.IsAny<IQueryable<User>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)admin);

            var handler = new PromoteUserCommandHandler(context.Object, queries.Object);
            var result = await handler.Handle(new PromoteUserCommand(1), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal("User is already an Admin.", result.ErrorMessage);
        }

        [Fact]
        public async Task DemoteUserCommandHandler_WhenNotAdminOrLastAdmin_ReturnsFailureInsteadOfThrowing()
        {
            var regular = User.Create("user@test.com", "hash", "Reg", "User", "User");
            var admin = User.Create("admin@test.com", "hash", "Admin", "User", "Admin");

            var context = new Mock<IApplicationDbContext>();
            context.SetupGet(c => c.Users).Returns(new[] { admin }.AsQueryable());

            var queries = new Mock<IQueryExecutor>();
            queries.SetupSequence(q => q.FirstOrDefaultAsync(It.IsAny<IQueryable<User>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)regular)
                .ReturnsAsync((User?)admin);
            queries.Setup(q => q.ToListAsync(It.IsAny<IQueryable<User>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([admin]);

            var handler = new DemoteUserCommandHandler(context.Object, queries.Object);

            var notAdminResult = await handler.Handle(new DemoteUserCommand(1), CancellationToken.None);
            Assert.False(notAdminResult.IsSuccess);
            Assert.Equal("User is not an Admin.", notAdminResult.ErrorMessage);

            var lastAdminResult = await handler.Handle(new DemoteUserCommand(2), CancellationToken.None);
            Assert.False(lastAdminResult.IsSuccess);
            Assert.Equal("Cannot demote the last remaining Admin. Promote another user first.", lastAdminResult.ErrorMessage);
        }
    }
}
