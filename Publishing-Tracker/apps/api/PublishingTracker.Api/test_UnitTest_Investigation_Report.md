using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using PublishingTracker.Api.Models.Dtos;
using Xunit;

namespace PublishingTracker.Api.Tests
{
    public class IntegrationTests : IClassFixture<TestWebAppFactory>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public IntegrationTests(TestWebAppFactory factory)
        {
            _factory = factory;
        }

        private async Task<HttpClient> GetAuthenticatedClientAsync()
        {
            var client = _factory.CreateClient();
            var loginRequest = new LoginRequest { Email = "test@test.com", Password = "password123" };
            var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            response.EnsureSuccessStatusCode();
            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);
            return client;
        }

        [Fact]
        public async Task Register_WithNewEmail_ReturnsOk()
        {
            var client = _factory.CreateClient();
            var registerRequest = new RegisterRequest { Email = "newuser@test.com", Password = "password123", FirstName = "New", LastName = "User" };
            var response = await client.PostAsJsonAsync("/api/auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
        {
            var client = _factory.CreateClient();
            var registerRequest = new RegisterRequest { Email = "test@test.com", Password = "password123", FirstName = "Test", LastName = "User" };
            var response = await client.PostAsJsonAsync("/api/auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsOk()
        {
            var client = _factory.CreateClient();
            var loginRequest = new LoginRequest { Email = "test@test.com", Password = "password123" };
            var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
        {
            var client = _factory.CreateClient();
            var loginRequest = new LoginRequest { Email = "test@test.com", Password = "wrongpassword" };
            var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetBooks_ReturnsOk()
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.GetAsync("/api/books");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PostBook_ReturnsCreated()
        {
            var client = await GetAuthenticatedClientAsync();
            var createBookDto = new CreateBookDto { Title = "New Book", Author = "Test Author" };
            var response = await client.PostAsJsonAsync("/api/books", createBookDto);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task GetPlatforms_ReturnsOk()
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.GetAsync("/api/platforms");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task RequestPlatform_ReturnsCreated()
        {
            var client = await GetAuthenticatedClientAsync();
            var requestDto = new PlatformRequestDto { Name = "New Platform", BaseUrl = "http://newplatform.com" };
            var response = await client.PostAsJsonAsync("/api/platforms/requests", requestDto);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }
}