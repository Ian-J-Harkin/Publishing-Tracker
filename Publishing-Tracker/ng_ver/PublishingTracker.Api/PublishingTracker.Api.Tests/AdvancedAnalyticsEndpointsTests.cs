using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using PublishingTracker.Api.Models.Dtos;
using Xunit;

namespace PublishingTracker.Api.Tests
{
    public class AdvancedAnalyticsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public AdvancedAnalyticsEndpointsTests(WebApplicationFactory<Program> factory)
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
        public async Task GetYoYComparison_ReturnsSuccessStatusCode()
        {
            // Arrange
            var client = await GetAuthenticatedClientAsync();

            // Act
            var response = await client.GetAsync("/api/dashboard/yoy");

            // Assert
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task GetSeasonalPerformance_ReturnsSuccessStatusCode()
        {
            // Arrange
            var client = await GetAuthenticatedClientAsync();

            // Act
            var response = await client.GetAsync("/api/dashboard/seasonal");

            // Assert
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task GetBookPerformance_ReturnsSuccessStatusCode()
        {
            // Arrange
            var client = await GetAuthenticatedClientAsync();

            // Act
            var response = await client.GetAsync("/api/books/1/performance");

            // Assert
            response.EnsureSuccessStatusCode();
        }
    }
}