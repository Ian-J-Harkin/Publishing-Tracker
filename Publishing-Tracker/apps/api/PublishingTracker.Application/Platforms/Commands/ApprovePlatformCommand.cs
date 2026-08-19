using MediatR;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Common.Interfaces;


namespace PublishingTracker.Application.Platforms.Commands;

public record ApprovePlatformCommand(int PlatformId) : IRequest<Result>;

public class ApprovePlatformCommandHandler : IRequestHandler<ApprovePlatformCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public ApprovePlatformCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(ApprovePlatformCommand request, CancellationToken cancellationToken)
    {
        var platform = await _context.Platforms.FindAsync(new object[] { request.PlatformId }, cancellationToken);
        if (platform == null)
            return Result.Failure("Platform not found.");

        platform.Approve();

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
