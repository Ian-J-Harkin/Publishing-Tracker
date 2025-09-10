using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using PublishingTracker.Api.Models.Dtos;
using Xunit;

namespace PublishingTracker.Api.Tests
{
    public class PlatformEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public PlatformEndpointsTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        private async Task<HttpClient> GetAuthenticatedClientAsync()
        {
            var client = _factory.CreateClient();
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "test@test.com", Password = "password123" });
            response.EnsureSuccessStatusCode();
            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);
            return client;
        }

        [Fact]
        public async Task GetPlatforms_ReturnsSuccessStatusCode()
        {
            // Arrange
            var client = await GetAuthenticatedClientAsync();

            // Act
            var response = await client.GetAsync("/api/platforms");

            // Assert
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task RequestPlatform_ReturnsCreatedStatusCode()
        {
            // Arrange
            var client = await GetAuthenticatedClientAsync();
            var newPlatformRequest = new PlatformRequestDto
            {
                Name = "New Platform",
                BaseUrl = "https://newplatform.com",
                CommissionRate = 0.1m
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/platforms/requests", newPlatformRequest);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }
}