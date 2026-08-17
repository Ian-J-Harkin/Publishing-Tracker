using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Api.Models;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Api.Domain.Entities;
using Microsoft.Extensions.Hosting;

namespace PublishingTracker.Api.Tests
{
    public class TestWebAppFactory : WebApplicationFactory<Program>
    {
        public TestWebAppFactory()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                var dbContextOptionsDescriptors = services.Where(d => d.ServiceType == typeof(DbContextOptions<PublishingTrackerDbContext>)).ToList();
                Console.WriteLine($"[TestWebAppFactory] Found {dbContextOptionsDescriptors.Count} DbContextOptions<PublishingTrackerDbContext> descriptors to remove.");
                foreach (var descriptor in dbContextOptionsDescriptors)
                {
                    services.Remove(descriptor);
                }

                var dbContextOptionsNonGenericDescriptors = services.Where(d => d.ServiceType == typeof(DbContextOptions)).ToList();
                Console.WriteLine($"[TestWebAppFactory] Found {dbContextOptionsNonGenericDescriptors.Count} DbContextOptions (non-generic) descriptors to remove.");
                foreach (var descriptor in dbContextOptionsNonGenericDescriptors)
                {
                    services.Remove(descriptor);
                }

                var dbContextDescriptors = services.Where(d => d.ServiceType == typeof(PublishingTrackerDbContext)).ToList();
                Console.WriteLine($"[TestWebAppFactory] Found {dbContextDescriptors.Count} PublishingTrackerDbContext descriptors to remove.");
                foreach (var descriptor in dbContextDescriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<PublishingTrackerDbContext>(options =>
                {
                    options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PublishingTracker_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=true");
                });


            });
        }

        private static readonly object _dbLock = new object();
        private static bool _dbInitialized = false;

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);
            
            lock (_dbLock)
            {
                if (!_dbInitialized)
                {
                    using (var scope = host.Services.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<PublishingTrackerDbContext>();
                        
                        // Ensure a clean slate for the test run
                        dbContext.Database.EnsureDeleted();
                        dbContext.Database.Migrate();

                        if (!dbContext.Users.Any(u => u.Email == "test@test.com"))
                        {
                            var passHash = BCrypt.Net.BCrypt.HashPassword("password123", BCrypt.Net.BCrypt.GenerateSalt(), false, BCrypt.Net.HashType.SHA256);
                            dbContext.Users.Add(User.Create("test@test.com", passHash, "Test", "User", "Admin"));
                            dbContext.SaveChanges();
                        }
                    }
                    _dbInitialized = true;
                }
            }
            return host;
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
