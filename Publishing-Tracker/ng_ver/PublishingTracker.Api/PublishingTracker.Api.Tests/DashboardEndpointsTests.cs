using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Services;
using Xunit;
using System.Net.Http.Headers;

namespace PublishingTracker.Api.Tests
{
    public class DashboardEndpointsTests : IClassFixture<TestWebAppFactory>
    {
        private readonly TestWebAppFactory _factory;

        public DashboardEndpointsTests(TestWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetSummary_ReturnsOk()
        {
            // Arrange
            var client = await _factory.GetAuthenticatedClientAsync();

            // Act
            var response = await client.GetAsync("/api/dashboard/summary");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}