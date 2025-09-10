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
using BCrypt.Net;

namespace PublishingTracker.Api.Tests
{
    public class BookEndpointsTests : IClassFixture<TestWebAppFactory>
    {
        private readonly TestWebAppFactory _factory;

        public BookEndpointsTests(TestWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task PostBook_ReturnsCreated()
        {
            // Arrange
            var client = await _factory.GetAuthenticatedClientAsync();
            var newBook = new CreateBookDto
            {
                Title = "Test Book",
                Author = "Test Author"
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/books", newBook);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task GetBooks_ReturnsOk()
        {
            // Arrange
            var client = await _factory.GetAuthenticatedClientAsync();

            // Act
            var response = await client.GetAsync("/api/books");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}