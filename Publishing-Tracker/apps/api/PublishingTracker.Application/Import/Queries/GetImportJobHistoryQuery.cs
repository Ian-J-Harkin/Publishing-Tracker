using MediatR;
using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Application.Common.Extensions;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Domain;

namespace PublishingTracker.Application.Import.Queries;

public record GetImportJobHistoryQuery(int UserId) : IRequest<Result<List<ImportJobDto>>>;

public class GetImportJobHistoryQueryHandler : IRequestHandler<GetImportJobHistoryQuery, Result<List<ImportJobDto>>>
{
    private readonly IApplicationDbContext _db;
    private readonly IQueryExecutor _queries;

    public GetImportJobHistoryQueryHandler(IApplicationDbContext db, IQueryExecutor queries)
    {
        _db = db;
        _queries = queries;
    }

    public async Task<Result<List<ImportJobDto>>> Handle(GetImportJobHistoryQuery request, CancellationToken cancellationToken)
    {
        var history = await _queries.ToListAsync(_db.ImportJobs
            .Where(j => j.UserId == request.UserId)
            .OrderByDescending(j => j.StartedAt)
            , cancellationToken);

        return Result<List<ImportJobDto>>.Success(history.Select(j => j.ToDto()).ToList());
    }
}
