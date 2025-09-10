using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PublishingTracker.Api.Tests
{
    public class ImportEndpointsTests : IClassFixture<TestWebAppFactory>
    {
        private readonly TestWebAppFactory _factory;

        public ImportEndpointsTests(TestWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task UploadFile_ReturnsOk()
        {
            // Arrange
            var client = await _factory.GetAuthenticatedClientAsync();
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(new byte[0]);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(fileContent, "file", "test.csv");

            // Act
            var response = await client.PostAsync("/api/import/upload", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}