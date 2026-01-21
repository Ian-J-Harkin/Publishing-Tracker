using Moq;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;
using PublishingTracker.Api.Services;
using Xunit;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;

namespace PublishingTracker.Api.Tests
{
    public class AuthServiceTests : IDisposable
    {
        private readonly PublishingTrackerDbContext _context;
        private readonly Mock<ITokenService> _mockTokenService;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            var options = new DbContextOptionsBuilder<PublishingTrackerDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new PublishingTrackerDbContext(options);
            _mockTokenService = new Mock<ITokenService>();
            _authService = new AuthService(_context, _mockTokenService.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public async Task RegisterAsync_WithValidUser_ReturnsAuthResponse()
        {
            // Arrange
            var request = new RegisterRequest { Email = "test@example.com", Password = "Password123", FirstName = "Test", LastName = "User" };
            _mockTokenService.Setup(s => s.CreateToken(It.IsAny<User>())).Returns("test_token");

            // Act
            var result = await _authService.RegisterAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test_token", result.Token);
            Assert.Equal(request.Email, result.Email);
        }

        [Fact]
        public async Task LoginAsync_WithValidCredentials_ReturnsAuthResponse()
        {
            // Arrange
            var request = new LoginRequest { Email = "test@example.com", Password = "Password123" };
            var user = new User { Email = request.Email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), FirstName = "Test", LastName = "User" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            _mockTokenService.Setup(s => s.CreateToken(It.IsAny<User>())).Returns("test_token");

            // Act
            var result = await _authService.LoginAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test_token", result.Token);
            Assert.Equal(user.Email, result.Email);
        }

        [Fact]
        public async Task RegisterAsync_WithExistingUser_ReturnsNull()
        {
            // Arrange
            var request = new RegisterRequest { Email = "test@example.com", Password = "Password123", FirstName = "Test", LastName = "User" };
            var user = new User { Email = request.Email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), FirstName = "Test", LastName = "User" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _authService.RegisterAsync(request);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task LoginAsync_WithInvalidCredentials_ReturnsNull()
        {
            // Arrange
            var request = new LoginRequest { Email = "test@example.com", Password = "Password123" };
            var user = new User { Email = request.Email, PasswordHash = BCrypt.Net.BCrypt.HashPassword("WrongPassword"), FirstName = "Test", LastName = "User" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _authService.LoginAsync(request);

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