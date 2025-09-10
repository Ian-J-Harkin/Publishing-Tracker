using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;
using PublishingTracker.Api.Services;
using Xunit;
using System.Net.Http.Headers;

namespace PublishingTracker.Api.Tests
{
    public class SalesEndpointsTests : IClassFixture<TestWebAppFactory>
    {
        private readonly TestWebAppFactory _factory;

        public SalesEndpointsTests(TestWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetSales_ReturnsOk()
        {
            // Arrange
            var client = await _factory.GetAuthenticatedClientAsync();

            // Act
            var response = await client.GetAsync("/api/sales");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}