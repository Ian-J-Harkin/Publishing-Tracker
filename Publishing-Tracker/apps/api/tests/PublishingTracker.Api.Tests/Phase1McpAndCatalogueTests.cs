using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Moq;
using PublishingTracker.Application.Books.Queries;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Infrastructure.Persistence;

namespace PublishingTracker.Api.Tests;

public sealed class Phase1McpAndCatalogueTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private static readonly SemaphoreSlim SeedLock = new(1, 1);
    private static SeedCatalogUsers? _seed;

    private readonly TestWebAppFactory _factory;
    private Dictionary<string, int> _bookIds = new();

    public Phase1McpAndCatalogueTests(TestWebAppFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await SeedLock.WaitAsync();
        try
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PublishingTrackerDbContext>();
            _seed ??= await TwoUserSeedCatalog.SeedAsync(db);

            _bookIds = await db.Books
                .Where(b => b.UserId == _seed.UserA.Id || b.UserId == _seed.UserB.Id)
                .ToDictionaryAsync(b => b.Title, b => b.Id);
        }
        finally
        {
            SeedLock.Release();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task McpEndpoint_WithoutBearerToken_Returns401Unauthorized()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"anon","version":"1.0"}}}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task McpClient_ListTools_DiscoversOnlyExpectedReadOnlyToolsWithoutTenantParams()
    {
        await using var mcp = await ConnectAsync(_seed!.UserA);

        var tools = await mcp.ListToolsAsync();

        Assert.Equal(
            ["find_titles_missing_metadata", "get_title_details"],
            tools.Select(t => t.Name).OrderBy(n => n).ToArray());

        foreach (var tool in tools)
        {
            Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint);
            Assert.False(tool.ProtocolTool.Annotations?.DestructiveHint);

            var schema = tool.ProtocolTool.InputSchema.ToString();
            Assert.DoesNotContain("userId", schema, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ownerId", schema, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("callerUserId", schema, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task McpClient_FindTitlesMissingMetadata_ReturnsIncompleteUserABooksAndRespectsFilters()
    {
        await using var mcp = await ConnectAsync(_seed!.UserA);

        var all = await FindAsync(mcp);
        Assert.Equal(3, all.TotalCount);
        var titles = all.Items.Select(i => i.Title).ToList();
        Assert.DoesNotContain("Complete Catalogue Title", titles);
        Assert.DoesNotContain("Free Promo Ebook", titles);
        Assert.DoesNotContain("Beta Private Incomplete Hardcover", titles);
        Assert.Contains(all.Items, i => i.Title == "Draft With No Editions" && i.MissingBookFields.Contains("Editions"));

        var audio = await FindAsync(mcp, new() { ["format"] = "Audiobook" });
        var audioItem = Assert.Single(audio.Items);
        Assert.Equal("Audio Missing Narrator", audioItem.Title);
        Assert.Equal(["Narrator", "Duration"], audioItem.Editions.Single().MissingFields);

        var series = await FindAsync(mcp, new() { ["missingField"] = "SeriesNumber" });
        var seriesItem = Assert.Single(series.Items);
        Assert.Equal("Clockwork Missing Book Metadata", seriesItem.Title);
        Assert.Equal(_bookIds["Clockwork Missing Book Metadata"], seriesItem.BookId);
    }

    [Fact]
    public async Task McpClient_FindTitlesMissingMetadata_PaginatesCorrectlyWithHasMore()
    {
        await using var mcp = await ConnectAsync(_seed!.UserA);

        var first = await FindAsync(mcp, new() { ["page"] = 1, ["pageSize"] = 2 });
        Assert.Equal(3, first.TotalCount);
        Assert.True(first.HasMore);
        Assert.Equal(2, first.Items.Count);

        var second = await FindAsync(mcp, new() { ["page"] = 2, ["pageSize"] = 2 });
        Assert.Equal(3, second.TotalCount);
        Assert.False(second.HasMore);
        Assert.Single(second.Items);

        Assert.Empty(first.Items.Select(i => i.BookId).Intersect(second.Items.Select(i => i.BookId)));
    }

    [Fact]
    public async Task McpClient_GetTitleDetails_ReturnsFullBookEditionsAndPlatformSummary()
    {
        await using var mcp = await ConnectAsync(_seed!.UserA);

        var result = await mcp.CallToolAsync(
            "get_title_details",
            new Dictionary<string, object?> { ["bookId"] = _bookIds["Audio Missing Narrator"] });

        Assert.NotEqual(true, result.IsError);
        var details = Deserialize<TitleDetailsDto>(result);
        Assert.Equal("Audio Missing Narrator", details.Title);
        Assert.Equal("Author Alpha", details.Author);
        Assert.Equal("Sci-Fi", details.Genre);
        Assert.Empty(details.MissingBookFields);

        var edition = Assert.Single(details.Editions);
        Assert.Equal("Audiobook", edition.Format);
        Assert.Equal(19.99m, edition.BasePrice);
        Assert.Equal(["Narrator", "Duration"], edition.MissingFields);

        var platform = Assert.Single(edition.PlatformSummaries);
        Assert.Equal("Kindle Direct", platform.PlatformName);
        Assert.Equal("B0AUDIO0001", platform.PlatformProductId);
        Assert.False(platform.IsPublished);
    }

    [Fact]
    public async Task McpClient_GetTitleDetails_TwoUserIsolation_MasksOtherUsersBook()
    {
        await using var mcp = await ConnectAsync(_seed!.UserA);

        var foreign = await CallAsync(mcp, "get_title_details", new() { ["bookId"] = _bookIds["Beta Private Incomplete Hardcover"] });
        var missing = await CallAsync(mcp, "get_title_details", new() { ["bookId"] = 999999 });

        Assert.True(foreign.IsError);
        Assert.True(missing.IsError);
        Assert.Contains("NOT_FOUND_OR_UNAUTHORIZED", foreign.Message);
        Assert.Equal(missing.Message, foreign.Message);
        Assert.DoesNotContain("Beta Private", foreign.Message);

        // User B can still read their own title, proving the denial above is ownership-based.
        await using var mcpB = await ConnectAsync(_seed!.UserB);
        var own = await mcpB.CallToolAsync(
            "get_title_details",
            new Dictionary<string, object?> { ["bookId"] = _bookIds["Beta Private Incomplete Hardcover"] });
        Assert.NotEqual(true, own.IsError);
    }

    [Theory]
    [InlineData("find_titles_missing_metadata", "page", 0, "Page must be greater than or equal to 1")]
    [InlineData("find_titles_missing_metadata", "pageSize", 51, "PageSize must be between 1 and 50")]
    [InlineData("find_titles_missing_metadata", "format", "Laserdisc", "Unsupported format 'Laserdisc'")]
    [InlineData("find_titles_missing_metadata", "format", "1", "Unsupported format '1'")]
    [InlineData("find_titles_missing_metadata", "missingField", "Bogus", "Unsupported missingField 'Bogus'")]
    [InlineData("get_title_details", "bookId", 0, "bookId must be a positive integer")]
    public async Task McpClient_InvalidArguments_ReturnsActionableErrors(string tool, string argument, object value, string expectedFragment)
    {
        await using var mcp = await ConnectAsync(_seed!.UserA);

        var arguments = new Dictionary<string, object?> { [argument] = value };
        if (tool == "get_title_details" && !arguments.ContainsKey("bookId")) arguments["bookId"] = 1;

        var outcome = await CallAsync(mcp, tool, arguments);

        Assert.True(outcome.IsError);
        Assert.Contains("INVALID_ARGUMENT", outcome.Message);
        Assert.Contains(expectedFragment, outcome.Message);
    }

    [Fact]
    public async Task RestEndpoints_MissingMetadataAndDetails_MatchMcpContractsAndIsolation()
    {
        using var clientA = CreateAuthenticatedClient(_seed!.UserA);

        var okResponse = await clientA.GetAsync("/api/books/missing-metadata?pageSize=2");
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);
        var page = await okResponse.Content.ReadFromJsonAsync<MissingMetadataPageDto>(JsonSerializerOptions.Web);
        Assert.Equal(3, page!.TotalCount);
        Assert.True(page.HasMore);
        Assert.Equal(2, page.Items.Count);

        Assert.Equal(HttpStatusCode.BadRequest, (await clientA.GetAsync("/api/books/missing-metadata?pageSize=51")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await clientA.GetAsync("/api/books/missing-metadata?format=Laserdisc")).StatusCode);

        var ownDetails = await clientA.GetAsync($"/api/books/{_bookIds["Audio Missing Narrator"]}/details");
        Assert.Equal(HttpStatusCode.OK, ownDetails.StatusCode);

        var foreignDetails = await clientA.GetAsync($"/api/books/{_bookIds["Beta Private Incomplete Hardcover"]}/details");
        var absentDetails = await clientA.GetAsync("/api/books/999999/details");
        Assert.Equal(HttpStatusCode.NotFound, foreignDetails.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, absentDetails.StatusCode);
        Assert.Equal(await absentDetails.Content.ReadAsStringAsync(), await foreignDetails.Content.ReadAsStringAsync());

        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/books/missing-metadata")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/books/1/details")).StatusCode);
    }

    [Fact]
    public async Task DeleteBook_AcrossTenants_ReturnsResultFailureAccessDenied_WithoutThrowing()
    {
        var betaBook = new PublishingTracker.Domain.Entities.Book(_seed!.UserB.Id, "User B Protected Book", "Author B");
        var repo = new Mock<IBookRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublishingTracker.Domain.Entities.Book?)betaBook);

        var currentUser = new Mock<PublishingTracker.Application.Common.Interfaces.ICurrentUserService>();
        currentUser.SetupGet(u => u.UserId).Returns(_seed.UserA.Id);

        var handler = new PublishingTracker.Application.Books.Commands.DeleteBookCommandHandler(repo.Object, currentUser.Object);
        var result = await handler.Handle(new PublishingTracker.Application.Books.Commands.DeleteBookCommand(99), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Access denied.", result.ErrorMessage);
        repo.Verify(r => r.DeleteAsync(It.IsAny<PublishingTracker.Domain.Entities.Book>(), It.IsAny<CancellationToken>()), Times.Never);

        using var clientA = CreateAuthenticatedClient(_seed.UserA);
        var httpResponse = await clientA.DeleteAsync($"/api/books/{_bookIds["Beta Private Incomplete Hardcover"]}");
        Assert.NotEqual(HttpStatusCode.InternalServerError, httpResponse.StatusCode);
    }

    [Fact]
    public void ApiAssembly_ConfiguresUserSecretsId_ForLocalDevelopment()
    {
        var apiAttr = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<Microsoft.Extensions.Configuration.UserSecrets.UserSecretsIdAttribute>(typeof(Program).Assembly);
        Assert.NotNull(apiAttr);
        Assert.Equal("publishingtracker-api-dev-secrets", apiAttr!.UserSecretsId);
    }

    private HttpClient CreateAuthenticatedClient(PublishingTracker.Domain.Entities.User user)
    {
        using var scope = _factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateToken(user);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<McpClient> ConnectAsync(PublishingTracker.Domain.Entities.User user)
    {
        var httpClient = CreateAuthenticatedClient(user);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            ownsHttpClient: true);

        return await McpClient.CreateAsync(transport);
    }

    private static async Task<MissingMetadataPageDto> FindAsync(McpClient mcp, Dictionary<string, object?>? arguments = null)
    {
        var result = await mcp.CallToolAsync("find_titles_missing_metadata", arguments ?? new Dictionary<string, object?>());
        Assert.NotEqual(true, result.IsError);
        return Deserialize<MissingMetadataPageDto>(result);
    }

    private static T Deserialize<T>(CallToolResult result)
    {
        Assert.NotNull(result.StructuredContent);
        return result.StructuredContent!.Value.Deserialize<T>(JsonSerializerOptions.Web)!;
    }

    // Tool errors surface either as IsError results or as protocol exceptions depending on SDK version; normalise both.
    private static async Task<(bool IsError, string Message)> CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        try
        {
            var result = await mcp.CallToolAsync(tool, arguments);
            var text = string.Join(" ", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
            return (result.IsError == true, text);
        }
        catch (ModelContextProtocol.McpException ex)
        {
            return (true, ex.Message);
        }
    }
}
