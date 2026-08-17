using MediatR;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Api.Models;

namespace PublishingTracker.Api.Features.Users.Commands;

public record DemoteUserCommand(int TargetUserId) : IRequest<Result>;

public class DemoteUserCommandHandler : IRequestHandler<DemoteUserCommand, Result>
{
    private readonly PublishingTrackerDbContext _context;

    public DemoteUserCommandHandler(PublishingTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(DemoteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync(new object[] { request.TargetUserId }, cancellationToken);
        if (user == null)
            return Result.Failure("User not found.");

        // Query the total number of admins and pass to the domain.
        // The domain enforces the invariant that the last admin cannot be demoted.
        var totalAdminCount = await _context.Users.CountAsync(u => u.Role == "Admin", cancellationToken);

        user.Demote(totalAdminCount);

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
