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
            var client = await _factory.GetAuthenticatedClientAsync();

            var csvContent = "Book Title,Platform,Sale Date,Quantity,Unit Price,Currency,Order ID\n" +
                             "Test Book,Test Platform,2024-01-01,1,10.00,USD,ORD-PREVIEW";

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csvContent));
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
            content.Add(fileContent, "file", "preview_test.csv");

            var uploadResponse = await client.PostAsync("/api/import/upload", content);
            uploadResponse.EnsureSuccessStatusCode();

            var previewData = await uploadResponse.Content.ReadFromJsonAsync<PreviewDataDto>();
            Assert.NotNull(previewData);
            Assert.Equal("preview_test.csv", previewData.FileName);
            Assert.Contains("Book Title", previewData.Headers);
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

            // 2. PROCESS — now returns 202 Accepted and processes in background
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
            Assert.Equal(HttpStatusCode.Accepted, processResponse.StatusCode);

            var queuedJob = await processResponse.Content.ReadFromJsonAsync<ImportJobDto>();
            Assert.NotNull(queuedJob);
            Assert.Equal("Queued", queuedJob.Status);

            // 3. POLL for completion — background service should process quickly in tests
            ImportJobDto? importResult = null;
            var maxWait = TimeSpan.FromSeconds(10);
            var started = DateTime.UtcNow;

            while (DateTime.UtcNow - started < maxWait)
            {
                await Task.Delay(200);
                var statusResponse = await client.GetAsync($"/api/import/status/{queuedJob.Id}");
                statusResponse.EnsureSuccessStatusCode();
                importResult = await statusResponse.Content.ReadFromJsonAsync<ImportJobDto>();
                if (importResult?.Status == "Completed" || importResult?.Status == "Failed")
                    break;
            }

            Assert.NotNull(importResult);
            Assert.Equal("Completed", importResult.Status);
            Assert.Equal(2, importResult.RecordsProcessed);
            Assert.Equal(2, importResult.RecordsSuccessful);
            Assert.Equal(0, importResult.RecordsFailed);

            // 4. VERIFY DB State
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PublishingTrackerDbContext>();
                
                var sales = await db.Sales.ToListAsync();
                Assert.True(sales.Count >= 2, $"Expected at least 2 sales, but found {sales.Count}");

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
