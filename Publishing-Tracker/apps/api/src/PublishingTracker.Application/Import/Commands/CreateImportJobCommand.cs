using MediatR;
using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Application.Common.Extensions;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Import.Models;
using PublishingTracker.Domain;
using PublishingTracker.Domain.Entities;

namespace PublishingTracker.Application.Import.Commands;

public record CreateImportJobCommand(int UserId, string FileName, string FilePath, ColumnMappingDto Mapping) : IRequest<Result<ImportJobDto>>;

public class CreateImportJobCommandHandler : IRequestHandler<CreateImportJobCommand, Result<ImportJobDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IImportQueue _importQueue;

    public CreateImportJobCommandHandler(IApplicationDbContext db, IImportQueue importQueue)
    {
        _db = db;
        _importQueue = importQueue;
    }

    public async Task<Result<ImportJobDto>> Handle(CreateImportJobCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            return Result<ImportJobDto>.Failure("Invalid file name.");

        var trimmed = request.FileName.Trim();
        var safeFileName = Path.GetFileName(trimmed);
        if (!string.Equals(trimmed, safeFileName, StringComparison.Ordinal) ||
            trimmed.Contains("..", StringComparison.Ordinal) ||
            trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            trimmed is "." or "..")
        {
            return Result<ImportJobDto>.Failure("Invalid file name.");
        }

        var tempDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "temp_uploads"));
        var expectedUploadPath = Path.GetFullPath(Path.Combine(tempDir, $"{request.UserId}_{safeFileName}"));
        var systemTempDir = Path.GetFullPath(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var expectedSystemTempPath = Path.GetFullPath(Path.Combine(systemTempDir, safeFileName));
        var resolvedPath = Path.GetFullPath(request.FilePath);

        var isUploadFile = resolvedPath.StartsWith(tempDir + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                           string.Equals(Path.GetDirectoryName(resolvedPath), tempDir, StringComparison.Ordinal) &&
                           string.Equals(resolvedPath, expectedUploadPath, StringComparison.Ordinal);
        var isSystemTempFile = resolvedPath.StartsWith(systemTempDir + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                               string.Equals(Path.GetDirectoryName(resolvedPath), systemTempDir, StringComparison.Ordinal) &&
                               string.Equals(resolvedPath, expectedSystemTempPath, StringComparison.Ordinal);

        if (!isUploadFile && !isSystemTempFile)
            return Result<ImportJobDto>.Failure("Invalid file path.");

        if (!File.Exists(resolvedPath))
            return Result<ImportJobDto>.Failure("Uploaded file not found. Please upload again.");

        var job = ImportJob.Create(request.UserId, safeFileName);
        _db.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        await _importQueue.EnqueueAsync(new ImportRequest(request.UserId, job.Id, resolvedPath, safeFileName, request.Mapping), cancellationToken);

        return Result<ImportJobDto>.Success(job.ToDto());
    }
}
