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
        if (!File.Exists(request.FilePath))
            return Result<ImportJobDto>.Failure("Uploaded file not found. Please upload again.");

        var job = ImportJob.Create(request.UserId, request.FileName);
        _db.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        await _importQueue.EnqueueAsync(new ImportRequest(request.UserId, job.Id, request.FilePath, request.FileName, request.Mapping), cancellationToken);

        return Result<ImportJobDto>.Success(job.ToDto());
    }
}
