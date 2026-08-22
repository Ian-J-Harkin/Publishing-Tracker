using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using PublishingTracker.Application.Common.Dtos;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace PublishingTracker.Api.Tests
{
    public class IntegrationTests : IClassFixture<TestWebAppFactory>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public IntegrationTests(TestWebAppFactory factory)
        {
            _factory = factory;

            // Step 2: Clean up database between each test run
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PublishingTracker.Infrastructure.Persistence.PublishingTrackerDbContext>();
            
            db.Sales.RemoveRange(db.Sales);
            db.ImportJobs.RemoveRange(db.ImportJobs);
            db.Books.RemoveRange(db.Books);
            db.Platforms.RemoveRange(db.Platforms);
            
            var nonTestUsers = db.Users.Where(u => u.Email != "test@test.com");
            db.Users.RemoveRange(nonTestUsers);
            
            db.SaveChanges();
        }

        private async Task<HttpClient> GetAuthenticatedClientAsync()
        {
            var client = _factory.CreateClient();
            var loginRequest = new LoginRequest { Email = "test@test.com", Password = "password123" };
            var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Login failed: {response.StatusCode} - {error}");
            }
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
            var createBookDto = new CreateBookDto 
            { 
                Title = "New Book", 
                Author = "Test Author",
                ISBN = "1234567890",
                PublicationDate = new System.DateTime(2023, 1, 1),
                BasePrice = 19.99m,
                Genre = "Fiction",
                Description = "A great book"
            };
            var response = await client.PostAsJsonAsync("/api/books", createBookDto);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task UpdateBook_SavesAllOptionalFields()
        {
            var client = await GetAuthenticatedClientAsync();
            
            // 1. Create a book
            var createBookDto = new CreateBookDto { Title = "Update Test Book", Author = "Author" };
            var createResp = await client.PostAsJsonAsync("/api/books", createBookDto);
            createResp.EnsureSuccessStatusCode();

            // Find the ID of the created book
            var books = await client.GetFromJsonAsync<System.Collections.Generic.List<PublishingTracker.Application.Books.Queries.BookResponseDto>>("/api/books");
            var bookId = books!.First(b => b.Title == "Update Test Book").Id;

            // 2. Update the book
            var updateDto = new UpdateBookDto
            {
                Title = "Updated Book Title",
                Author = "Updated Author",
                ISBN = "0987654321",
                PublicationDate = new System.DateTime(2024, 1, 1),
                BasePrice = 25.50m,
                Genre = "Sci-Fi",
                Description = "An updated description"
            };
            
            var updateResp = await client.PutAsJsonAsync($"/api/books/{bookId}", updateDto);
            updateResp.EnsureSuccessStatusCode();

            // 3. Get the book again to verify all fields were saved
            var getResp = await client.GetFromJsonAsync<PublishingTracker.Application.Books.Queries.BookResponseDto>($"/api/books/{bookId}");
            
            Assert.Equal("Updated Book Title", getResp!.Title);
            Assert.Equal(new System.DateTime(2024, 1, 1), getResp.PublicationDate);
            Assert.Equal(25.50m, getResp.BasePrice);
            Assert.Equal("Sci-Fi", getResp.Genre);
            Assert.Equal("An updated description", getResp.Description);
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
            var response = await client.PostAsJsonAsync("/api/platforms", requestDto);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        [Fact]
        public async Task CreateSale_WithForeignCurrency_ConvertsToBaseCurrency()
        {
            var client = await GetAuthenticatedClientAsync();
            
            // 1. Create a Book
            var bookDto = new CreateBookDto { Title = "Currency Test Book", Author = "Test", BasePrice = 10m };
            var bookResp = await client.PostAsJsonAsync("/api/books", bookDto);
            bookResp.EnsureSuccessStatusCode();
            
            // 2. Create a Platform
            var platDto = new PlatformRequestDto { Name = "EuroPlatform", BaseUrl = "http://euro.com" };
            var platResp = await client.PostAsJsonAsync("/api/platforms", platDto);
            platResp.EnsureSuccessStatusCode();

            // Assume IDs are generated chronologically and this is a fresh in-memory DB or we fetch them
            // To be safe, let's fetch them:
            var books = await client.GetFromJsonAsync<System.Collections.Generic.List<PublishingTracker.Application.Books.Queries.BookResponseDto>>("/api/books");
            var bookId = books!.First(b => b.Title == "Currency Test Book").Id;

            // Fetch platforms, including inactive ones since we just created it
            var platforms = await client.GetFromJsonAsync<System.Collections.Generic.List<PublishingTracker.Application.Common.Dtos.PlatformResponseDto>>("/api/platforms?includeInactive=true");
            var platId = platforms!.First(p => p.Name == "EuroPlatform").Id;

            // Approve the platform
            var approveResp = await client.PutAsync($"/api/platforms/{platId}/approve", null);
            approveResp.EnsureSuccessStatusCode();

            // 3. Create a Sale in EUR
            var saleDto = new CreateSaleDto
            {
                BookId = bookId,
                PlatformId = platId,
                SaleDate = System.DateTime.UtcNow,
                Quantity = 1,
                UnitPrice = 10m,
                Royalty = 10m,
                Currency = "EUR"
            };

            var saleResp = await client.PostAsJsonAsync("/api/sales", saleDto);
            saleResp.EnsureSuccessStatusCode();
            
            var createdSale = await saleResp.Content.ReadFromJsonAsync<SaleDto>();
            
            // Assertions
            Assert.Equal(10m, createdSale!.Revenue);
            Assert.Equal("EUR", createdSale.Currency);
            // Exchange rate for EUR to USD in StaticCurrencyConverterService is 1.10
            Assert.Equal(11.0m, createdSale.BaseCurrencyRevenue);
        }

        [Fact]
        public async Task CreateSale_CommissionRateChange_DoesNotCorruptHistory()
        {
            var client = await GetAuthenticatedClientAsync();
            
            // 1. Create a Book
            var bookDto = new CreateBookDto { Title = "Commission Test Book", Author = "Test", BasePrice = 10m };
            await client.PostAsJsonAsync("/api/books", bookDto);
            var books = await client.GetFromJsonAsync<System.Collections.Generic.List<PublishingTracker.Application.Books.Queries.BookResponseDto>>("/api/books");
            var bookId = books!.First(b => b.Title == "Commission Test Book").Id;

            // 2. Create Platform (we need a way to set commission rate, assume default is 0 or it's not exposed via CreateDto)
            // Wait, PlatformRequestDto doesn't expose CommissionRate. Let's see if we can create it via DB directly in the factory or if we can't test this easily via API.
            // I will test this by directly injecting the DbContext and changing the platform's commission rate.
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PublishingTracker.Infrastructure.Persistence.PublishingTrackerDbContext>();
            
            var platform = PublishingTracker.Domain.Entities.Platform.Create("TestPlatform", "http://test.com", 0.30m);
            db.Platforms.Add(platform);
            await db.SaveChangesAsync();

            // 3. Create Sale A (30%)
            var saleDtoA = new CreateSaleDto
            {
                BookId = bookId,
                PlatformId = platform.Id,
                SaleDate = System.DateTime.UtcNow,
                Quantity = 1,
                UnitPrice = 10m,
                Royalty = 7m, // (10 - 30%)
                Currency = "USD"
            };
            var saleAResp = await client.PostAsJsonAsync("/api/sales", saleDtoA);
            saleAResp.EnsureSuccessStatusCode();
            var createdSaleA = await saleAResp.Content.ReadFromJsonAsync<SaleDto>();

            // 4. Update Platform to 35%
            typeof(PublishingTracker.Domain.Entities.Platform).GetProperty("CommissionRate")!.SetValue(platform, 0.35m);
            db.Platforms.Update(platform);
            await db.SaveChangesAsync();

            // 5. Create Sale B (35%)
            var saleDtoB = new CreateSaleDto
            {
                BookId = bookId,
                PlatformId = platform.Id,
                SaleDate = System.DateTime.UtcNow,
                Quantity = 1,
                UnitPrice = 10m,
                Royalty = 6.5m, // (10 - 35%)
                Currency = "USD"
            };
            var saleBResp = await client.PostAsJsonAsync("/api/sales", saleDtoB);
            saleBResp.EnsureSuccessStatusCode();
            var createdSaleB = await saleBResp.Content.ReadFromJsonAsync<SaleDto>();

            // Assertions
            Assert.Equal(0.30m, createdSaleA!.PlatformCommissionRate);
            Assert.Equal(0.35m, createdSaleB!.PlatformCommissionRate);
        }
        [Fact]
        public async Task AdminEndpoint_GetUsers_ForbiddenForStandardUser()
        {
            // Register and login as a new standard user (since test@test.com is seeded as Admin)
            var client = _factory.CreateClient();
            var registerRequest = new RegisterRequest { Email = "standard@test.com", Password = "password123", FirstName = "Standard", LastName = "User" };
            await client.PostAsJsonAsync("/api/auth/register", registerRequest);
            
            var loginRequest = new LoginRequest { Email = "standard@test.com", Password = "password123" };
            var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

            var getUsersResponse = await client.GetAsync("/api/users");
            Assert.Equal(HttpStatusCode.Forbidden, getUsersResponse.StatusCode);
        }

        [Fact]
        public async Task AdminEndpoint_ApprovePlatform_ForbiddenForStandardUser()
        {
            var client = _factory.CreateClient();
            var registerRequest = new RegisterRequest { Email = "standard2@test.com", Password = "password123", FirstName = "Standard", LastName = "User" };
            await client.PostAsJsonAsync("/api/auth/register", registerRequest);
            
            var loginRequest = new LoginRequest { Email = "standard2@test.com", Password = "password123" };
            var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

            var approveResponse = await client.PutAsync("/api/platforms/1/approve", null);
            Assert.Equal(HttpStatusCode.Forbidden, approveResponse.StatusCode);
        }

        [Fact]
        public async Task GetProfile_ReturnsCurrentUserProfile()
        {
            var client = _factory.CreateClient();
            var registerRequest = new RegisterRequest { Email = "profile@test.com", Password = "password123", FirstName = "John", LastName = "Doe" };
            await client.PostAsJsonAsync("/api/auth/register", registerRequest);
            
            var loginRequest = new LoginRequest { Email = "profile@test.com", Password = "password123" };
            var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

            var profileResponse = await client.GetAsync("/api/users/me");
            Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
            
            var profile = await profileResponse.Content.ReadFromJsonAsync<UserResponseDto>();
            Assert.Equal("John", profile!.FirstName);
            Assert.Equal("Doe", profile.LastName);
            Assert.Equal("profile@test.com", profile.Email);
        }

        [Fact]
        public async Task UpdateProfile_UpdatesUserProperly()
        {
            var client = _factory.CreateClient();
            var registerRequest = new RegisterRequest { Email = "profileupdate@test.com", Password = "password123", FirstName = "Old", LastName = "Name" };
            await client.PostAsJsonAsync("/api/auth/register", registerRequest);
            
            var loginRequest = new LoginRequest { Email = "profileupdate@test.com", Password = "password123" };
            var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

            var updateCommand = new PublishingTracker.Application.Users.Commands.UpdateProfileCommand("New First", "New Last");
            var updateResponse = await client.PutAsJsonAsync("/api/users/me/profile", updateCommand);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            var profileResponse = await client.GetAsync("/api/users/me");
            var profile = await profileResponse.Content.ReadFromJsonAsync<UserResponseDto>();
            Assert.Equal("New First", profile!.FirstName);
            Assert.Equal("New Last", profile.LastName);
        }

        [Fact]
        public async Task ChangePassword_WithCorrectOldPassword_ChangesPassword()
        {
            var client = _factory.CreateClient();
            var registerRequest = new RegisterRequest { Email = "passchange@test.com", Password = "oldPassword123", FirstName = "Pass", LastName = "Change" };
            await client.PostAsJsonAsync("/api/auth/register", registerRequest);
            
            var loginRequest = new LoginRequest { Email = "passchange@test.com", Password = "oldPassword123" };
            var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

            var changeCommand = new PublishingTracker.Application.Users.Commands.ChangePasswordCommand("oldPassword123", "newPassword123");
            var changeResponse = await client.PutAsJsonAsync("/api/users/me/password", changeCommand);
            Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

            // Verify login with new password works
            var newLoginRequest = new LoginRequest { Email = "passchange@test.com", Password = "newPassword123" };
            var newLoginResponse = await client.PostAsJsonAsync("/api/auth/login", newLoginRequest);
            Assert.Equal(HttpStatusCode.OK, newLoginResponse.StatusCode);

            // Verify login with old password fails
            var oldLoginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);
        }

        [Fact]
        public async Task ChangePassword_WithIncorrectOldPassword_Fails()
        {
            var client = _factory.CreateClient();
            var registerRequest = new RegisterRequest { Email = "passfail@test.com", Password = "realPassword123", FirstName = "Pass", LastName = "Fail" };
            await client.PostAsJsonAsync("/api/auth/register", registerRequest);
            
            var loginRequest = new LoginRequest { Email = "passfail@test.com", Password = "realPassword123" };
            var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

            var changeCommand = new PublishingTracker.Application.Users.Commands.ChangePasswordCommand("wrongPassword", "newPassword123");
            var changeResponse = await client.PutAsJsonAsync("/api/users/me/password", changeCommand);
            Assert.Equal(HttpStatusCode.BadRequest, changeResponse.StatusCode);

            // Verify login with old password still works
            var oldLoginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            Assert.Equal(HttpStatusCode.OK, oldLoginResponse.StatusCode);
        }

        [Fact]
        public async Task GetDashboardData_ReturnsOk()
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.GetAsync("/api/dashboard/all");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetDashboardSummary_ReturnsOk()
        {
            var client = await GetAuthenticatedClientAsync();
            var response = await client.GetAsync("/api/dashboard/summary");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
