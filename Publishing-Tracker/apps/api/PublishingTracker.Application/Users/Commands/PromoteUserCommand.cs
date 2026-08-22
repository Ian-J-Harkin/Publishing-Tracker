using MediatR;
using PublishingTracker.Application.Common.Interfaces;


namespace PublishingTracker.Application.Users.Commands;

public record PromoteUserCommand(int TargetUserId) : IRequest<Result>;

public class PromoteUserCommandHandler : IRequestHandler<PromoteUserCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IQueryExecutor _queries;

    public PromoteUserCommandHandler(IApplicationDbContext context, IQueryExecutor queries)
    {
        _context = context;
        _queries = queries;
    }

    public async Task<Result> Handle(PromoteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _queries.FirstOrDefaultAsync(_context.Users.Where(u => u.Id == request.TargetUserId), cancellationToken);
        if (user == null)
            return Result.Failure("User not found.");

        user.Promote();

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
