using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using PublishingTracker.Api.Models.Dtos;
using Xunit;

namespace PublishingTracker.Api.Tests
{
    public class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public AuthControllerTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
            });
        }

        [Fact]
        public async Task Register_WithValidModel_ReturnsOk()
        {
            // Arrange
            var client = _factory.CreateClient();
            var model = new RegisterRequest { Email = "test@example.com", Password = "Password123", FirstName = "Test", LastName = "User" };

            // Act
            var response = await client.PostAsJsonAsync("/api/auth/register", model);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsOk()
        {
            // Arrange
            var client = _factory.CreateClient();
            var registerModel = new RegisterRequest { Email = "test2@example.com", Password = "Password123", FirstName = "Test", LastName = "User" };
            await client.PostAsJsonAsync("/api/auth/register", registerModel);
            var loginModel = new LoginRequest { Email = "test2@example.com", Password = "Password123" };

            // Act
            var response = await client.PostAsJsonAsync("/api/auth/login", loginModel);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Register_WithInvalidModel_ReturnsBadRequest()
        {
            // Arrange
            var client = _factory.CreateClient();
            var model = new RegisterRequest { Email = "test@example.com" };

            // Act
            var response = await client.PostAsJsonAsync("/api/auth/register", model);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var client = _factory.CreateClient();
            var model = new LoginRequest { Email = "test@example.com", Password = "WrongPassword" };

            // Act
            var response = await client.PostAsJsonAsync("/api/auth/login", model);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}