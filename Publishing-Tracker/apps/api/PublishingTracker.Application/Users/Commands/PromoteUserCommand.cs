using MediatR;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Common.Interfaces;


namespace PublishingTracker.Application.Users.Commands;

public record PromoteUserCommand(int TargetUserId) : IRequest<Result>;

public class PromoteUserCommandHandler : IRequestHandler<PromoteUserCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public PromoteUserCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(PromoteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync(new object[] { request.TargetUserId }, cancellationToken);
        if (user == null)
            return Result.Failure("User not found.");

        user.Promote();

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
