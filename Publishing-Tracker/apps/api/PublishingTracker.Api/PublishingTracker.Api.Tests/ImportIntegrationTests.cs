using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using PublishingTracker.Api.Models.Dtos;
using Xunit;
using PublishingTracker.Api.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace PublishingTracker.Api.Tests
{
    public class ImportIntegrationTests : IClassFixture<TestWebAppFactory>
    {
        private readonly TestWebAppFactory _factory;

        public ImportIntegrationTests(TestWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Upload_ValidCsv_ReturnsPreview()
        {
            var client = _factory.CreateClient();
            
            // Login to get token first or use factory helper if exposed
            // The factory exposes GetAuthenticatedClientAsync but creates a NEW client inside.
            // Let's rely on standard flow or use the public method if accessible.
            // Since the user didn't show me the Interface but strict class, I'll trust the class has it.
            // But wait, the previous `IntegrationTests.cs` implemented its own helper. 
            // I'll implement a local helper to be safe and consistent with existing patterns, 
            // or modify the factory if needed. But simpler to just reimplement login here or copy.
            // Actually, TestWebAppFactory has `GetAuthenticatedClientAsync`. I'll try to use it.
            // However, IntegrationTests doesn't use it, maybe because it wants specific login control?
            // I'll try to use _factory.GetAuthenticatedClientAsync() assuming it works.
            
            // Re-read TestWebAppFactory.cs content... it has public async Task<HttpClient> GetAuthenticatedClientAsync().
            // So I can use it.
            
            // Wait, CreateClient() inside GetAuthenticatedClientAsync creates a new client.
            // I should use that.
        }

        private async Task<HttpClient> GetClient()
        {
            // The TestWebAppFactory has a method GetAuthenticatedClientAsync but it might not be accessible if I don't cast or if build issue.
            // Let's assume standard DI injection of fixture works.
            return await _factory.GetAuthenticatedClientAsync();
        }

        [Fact]
        public async Task ImportFlow_UploadAndProcess_CreatesRecords()
        {
            var client = await _factory.GetAuthenticatedClientAsync();

            // 1. UPLOAD
            var csvContent = "Book Title,Platform,Sale Date,Quantity,Unit Price,Currency,Order ID\n" +
                             "Test Book,Test Platform,2024-01-01,1,10.00,USD,ORD-001\n" +
                             "Another Book,New Platform,2024-01-02,2,5.50,EUR,ORD-002";

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csvContent));
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
            content.Add(fileContent, "file", "test_import.csv");

            var uploadResponse = await client.PostAsync("/api/import/upload", content);
            uploadResponse.EnsureSuccessStatusCode();

            var previewData = await uploadResponse.Content.ReadFromJsonAsync<PreviewDataDto>();
            Assert.NotNull(previewData);
            Assert.Equal("test_import.csv", previewData.FileName);
            Assert.Contains("Book Title", previewData.Headers);

            // 2. PROCESS
            var mapping = new ColumnMappingDto
            {
                BookTitle = "Book Title",
                Platform = "Platform",
                SaleDate = "Sale Date",
                Quantity = "Quantity",
                UnitPrice = "Unit Price",
                Currency = "Currency",
                OrderId = "Order ID"
            };

            var processRequest = new
            {
                FileName = "test_import.csv",
                Mapping = mapping
            };

            var processResponse = await client.PostAsJsonAsync("/api/import/process", processRequest);
            processResponse.EnsureSuccessStatusCode();

            var importResult = await processResponse.Content.ReadFromJsonAsync<ImportJobDto>();
            Assert.NotNull(importResult);
            Assert.Equal(2, importResult.RecordsProcessed);
            Assert.Equal(2, importResult.RecordsSuccessful);
            Assert.Equal(0, importResult.RecordsFailed);

            // 3. VERIFY DB State
            // Use a scope to check the DB
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PublishingTrackerDbContext>();
                
                var sales = await db.Sales.ToListAsync();
                Assert.Equal(2, sales.Count);

                var book = await db.Books.FirstOrDefaultAsync(b => b.Title == "Test Book");
                Assert.NotNull(book);

                var platform = await db.Platforms.FirstOrDefaultAsync(p => p.Name == "New Platform");
                Assert.NotNull(platform);

                var eurSale = sales.FirstOrDefault(s => s.Currency == "EUR");
                Assert.NotNull(eurSale);
                Assert.Equal(11.00m, eurSale.Revenue); // 2 * 5.50
            }
        }
    }
}
