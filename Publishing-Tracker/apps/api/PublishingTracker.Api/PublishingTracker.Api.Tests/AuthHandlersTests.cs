using Moq;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Api.Models;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Api.Domain.Entities;
using PublishingTracker.Domain.Dtos;
using PublishingTracker.Infrastructure.Services;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Api.Features.Auth.Commands;
using PublishingTracker.Api.Features.Auth.Queries;
using Xunit;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;
using System.Threading;

namespace PublishingTracker.Api.Tests
{
    public class AuthHandlersTests : IDisposable
    {
        private readonly PublishingTrackerDbContext _context;
        private readonly Mock<ITokenService> _mockTokenService;
        private readonly RegisterUserCommandHandler _registerHandler;
        private readonly LoginUserQueryHandler _loginHandler;

        public AuthHandlersTests()
        {
            var options = new DbContextOptionsBuilder<PublishingTrackerDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new PublishingTrackerDbContext(options);
            _mockTokenService = new Mock<ITokenService>();
            
            _registerHandler = new RegisterUserCommandHandler(_context, _mockTokenService.Object);
            _loginHandler = new LoginUserQueryHandler(_context, _mockTokenService.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public async Task RegisterUserCommand_WithValidUser_ReturnsAuthResponse()
        {
            // Arrange
            var command = new RegisterUserCommand("test@example.com", "Password123", "Test", "User");
            _mockTokenService.Setup(s => s.CreateToken(It.IsAny<User>())).Returns("test_token");

            // Act
            var result = await _registerHandler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test_token", result.Token);
            Assert.Equal(command.Email, result.Email);
        }

        [Fact]
        public async Task LoginUserQuery_WithValidCredentials_ReturnsAuthResponse()
        {
            // Arrange
            var query = new LoginUserQuery("test@example.com", "Password123");
            var user = User.Create(query.Email, BCrypt.Net.BCrypt.HashPassword(query.Password), "Test", "User");
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            _mockTokenService.Setup(s => s.CreateToken(It.IsAny<User>())).Returns("test_token");

            // Act
            var result = await _loginHandler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test_token", result.Token);
            Assert.Equal(user.Email, result.Email);
        }

        [Fact]
        public async Task RegisterUserCommand_WithExistingUser_ReturnsNull()
        {
            // Arrange
            var command = new RegisterUserCommand("test@example.com", "Password123", "Test", "User");
            var user = User.Create(command.Email, BCrypt.Net.BCrypt.HashPassword(command.Password), "Test", "User");
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _registerHandler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task LoginUserQuery_WithInvalidCredentials_ReturnsNull()
        {
            // Arrange
            var query = new LoginUserQuery("test@example.com", "Password123");
            var user = User.Create(query.Email, BCrypt.Net.BCrypt.HashPassword("WrongPassword"), "Test", "User");
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _loginHandler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void VerifyPassword_WithCorrectPasswordAndHashType_ReturnsTrue()
        {
            // Arrange
            var password = "Password123";
            var salt = BCrypt.Net.BCrypt.GenerateSalt();
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password, salt, false, BCrypt.Net.HashType.SHA256);

            // Act
            var result = BCrypt.Net.BCrypt.Verify(password, hashedPassword, false, BCrypt.Net.HashType.SHA256);

            // Assert
            Assert.True(result);
        }
    }
}
