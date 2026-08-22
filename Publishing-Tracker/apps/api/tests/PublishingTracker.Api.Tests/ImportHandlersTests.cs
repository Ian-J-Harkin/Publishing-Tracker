using Microsoft.EntityFrameworkCore;
using Moq;
using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Import.Commands;
using PublishingTracker.Application.Import.Models;
using PublishingTracker.Application.Import.Queries;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Infrastructure.Persistence;

namespace PublishingTracker.Api.Tests;

public class ImportHandlersTests : IDisposable
{
    private readonly PublishingTrackerDbContext _context;
    private readonly IQueryExecutor _queries;
    private readonly Mock<IImportQueue> _queue;
    private readonly Mock<ICurrentUserService> _currentUser;

    public ImportHandlersTests()
    {
        var options = new DbContextOptionsBuilder<PublishingTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _currentUser = new Mock<ICurrentUserService>();
        _currentUser.SetupGet(x => x.UserId).Returns(42);
        _context = new PublishingTrackerDbContext(options, _currentUser.Object);
        _queries = new QueryExecutor();
        _queue = new Mock<IImportQueue>(MockBehavior.Strict);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task UploadImportFileCommand_ReturnsPreviewData()
    {
        var fileName = $"{Guid.NewGuid():N}.csv";
        await using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            "Book Title,Platform,Sale Date,Quantity\nTest Book,Store,2024-01-01,1\n"));

        var handler = new UploadImportFileCommandHandler();

        try
        {
            var result = await handler.Handle(new UploadImportFileCommand(7, fileName, input), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(fileName, result.Value!.FileName);
            Assert.Contains("Book Title", result.Value.Headers);
            Assert.Single(result.Value.PreviewRows);
            Assert.Equal("Test Book", result.Value.PreviewRows[0]["Book Title"]);
        }
        finally
        {
            var tempPath = Path.Combine(Directory.GetCurrentDirectory(), "temp_uploads", $"7_{fileName}");
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task CreateImportJobCommand_CreatesQueuedJob_AndEnqueuesWork()
    {
        var fileName = $"{Guid.NewGuid():N}.csv";
        var tempPath = Path.Combine(Path.GetTempPath(), fileName);
        await File.WriteAllTextAsync(tempPath, "Book Title,Platform\nBook,Platform");

        var handler = new CreateImportJobCommandHandler(_context, _queue.Object);
        var mapping = new ColumnMappingDto { BookTitle = "Book Title", Platform = "Platform" };

        _queue
            .Setup(q => q.EnqueueAsync(It.Is<ImportRequest>(r =>
                    r.UserId == 42 &&
                    r.FileName == fileName &&
                    r.FilePath == tempPath &&
                    r.Mapping.BookTitle == "Book Title"),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        try
        {
            var result = await handler.Handle(new CreateImportJobCommand(42, fileName, tempPath, mapping), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal("Queued", result.Value!.Status);
            Assert.Equal(fileName, result.Value.FileName);
            Assert.Equal(1, await _context.ImportJobs.CountAsync());
            _queue.VerifyAll();
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task GetImportJobStatusQuery_ReturnsJobForMatchingUser()
    {
        var job = ImportJob.Create(42, "status.csv");
        _context.ImportJobs.Add(job);
        await _context.SaveChangesAsync();

        var handler = new GetImportJobStatusQueryHandler(_context, _queries);

        var result = await handler.Handle(new GetImportJobStatusQuery(42, job.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(job.Id, result.Value!.Id);
        Assert.Equal("status.csv", result.Value.FileName);
    }

    [Fact]
    public async Task GetImportJobHistoryQuery_ReturnsJobsInDescendingOrder()
    {
        var first = ImportJob.Create(42, "first.csv");
        await Task.Delay(20);
        var second = ImportJob.Create(42, "second.csv");

        _context.ImportJobs.AddRange(first, second);
        await _context.SaveChangesAsync();

        var handler = new GetImportJobHistoryQueryHandler(_context, _queries);
        var result = await handler.Handle(new GetImportJobHistoryQuery(42), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("second.csv", result.Value[0].FileName);
        Assert.Equal("first.csv", result.Value[1].FileName);
    }
}
