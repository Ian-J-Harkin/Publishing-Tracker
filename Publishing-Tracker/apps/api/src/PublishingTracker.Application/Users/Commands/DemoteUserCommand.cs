using MediatR;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Domain.Exceptions;


namespace PublishingTracker.Application.Users.Commands;

public record DemoteUserCommand(int TargetUserId) : IRequest<Result>;

public class DemoteUserCommandHandler : IRequestHandler<DemoteUserCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IQueryExecutor _queries;

    public DemoteUserCommandHandler(IApplicationDbContext context, IQueryExecutor queries)
    {
        _context = context;
        _queries = queries;
    }

    public async Task<Result> Handle(DemoteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _queries.FirstOrDefaultAsync(_context.Users.Where(u => u.Id == request.TargetUserId), cancellationToken);
        if (user == null)
            return Result.Failure("User not found.");

        // Query the total number of admins and pass to the domain.
        // The domain enforces the invariant that the last admin cannot be demoted.
        var totalAdminCount = (await _queries.ToListAsync(_context.Users.Where(u => u.Role == "Admin"), cancellationToken)).Count;

        try
        {
            user.Demote(totalAdminCount);
        }
        catch (InvalidDomainStateException ex)
        {
            return Result.Failure(ex.Message);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
