using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Services;

namespace PublishingTracker.Api.Tests
{
    public class TestWebAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PublishingTrackerDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<PublishingTrackerDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestingDb");
                });

                var sp = services.BuildServiceProvider();
                using (var scope = sp.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<PublishingTrackerDbContext>();
                    db.Database.EnsureCreated();
                    if (!db.Users.Any(u => u.Email == "test@test.com"))
                    {
                        db.Users.Add(new User { Email = "test@test.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123", BCrypt.Net.BCrypt.GenerateSalt(), false, BCrypt.Net.HashType.SHA256) });
                        db.SaveChanges();
                    }
                }
            });
        }

        public async Task<HttpClient> GetAuthenticatedClientAsync()
        {
            var client = CreateClient();
            using (var scope = Services.CreateScope())
            {
                var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<PublishingTrackerDbContext>();
                var user = dbContext.Users.First(u => u.Email == "test@test.com");
                var token = tokenService.CreateToken(user);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var antiforgery = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
                var tokens = antiforgery.GetAndStoreTokens(new DefaultHttpContext());
                client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokens.RequestToken);
            }
            return client;
        }
    }
}