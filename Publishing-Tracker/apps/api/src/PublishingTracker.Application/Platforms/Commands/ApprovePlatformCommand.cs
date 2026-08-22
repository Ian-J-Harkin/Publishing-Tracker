using MediatR;
using PublishingTracker.Application.Common.Interfaces;


namespace PublishingTracker.Application.Platforms.Commands;

public record ApprovePlatformCommand(int PlatformId) : IRequest<Result>;

public class ApprovePlatformCommandHandler : IRequestHandler<ApprovePlatformCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IQueryExecutor _queries;

    public ApprovePlatformCommandHandler(IApplicationDbContext context, IQueryExecutor queries)
    {
        _context = context;
        _queries = queries;
    }

    public async Task<Result> Handle(ApprovePlatformCommand request, CancellationToken cancellationToken)
    {
        var platform = await _queries.FirstOrDefaultAsync(_context.Platforms.Where(p => p.Id == request.PlatformId), cancellationToken);
        if (platform == null)
            return Result.Failure("Platform not found.");

        platform.Approve();

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
