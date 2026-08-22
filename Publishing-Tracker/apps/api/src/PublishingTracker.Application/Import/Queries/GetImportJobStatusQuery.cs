using MediatR;
using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Application.Common.Extensions;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Domain;

namespace PublishingTracker.Application.Import.Queries;

public record GetImportJobStatusQuery(int UserId, int JobId) : IRequest<Result<ImportJobDto>>;

public class GetImportJobStatusQueryHandler : IRequestHandler<GetImportJobStatusQuery, Result<ImportJobDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IQueryExecutor _queries;

    public GetImportJobStatusQueryHandler(IApplicationDbContext db, IQueryExecutor queries)
    {
        _db = db;
        _queries = queries;
    }

    public async Task<Result<ImportJobDto>> Handle(GetImportJobStatusQuery request, CancellationToken cancellationToken)
    {
        var job = await _queries.FirstOrDefaultAsync(_db.ImportJobs.Where(j => j.Id == request.JobId && j.UserId == request.UserId), cancellationToken);
        if (job == null)
            return Result<ImportJobDto>.Failure("Import job not found.");

        return Result<ImportJobDto>.Success(job.ToDto());
    }
}
