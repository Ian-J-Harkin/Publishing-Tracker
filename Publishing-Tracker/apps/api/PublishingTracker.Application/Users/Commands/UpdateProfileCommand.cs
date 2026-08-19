using MediatR;

using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Common.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace PublishingTracker.Application.Users.Commands;

public record UpdateProfileCommand(string FirstName, string LastName) : IRequest<Result<bool>>;

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateProfileCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Result<bool>.Failure("Unauthorized");

        var user = await _context.Users.FindAsync(new object[] { userId.Value }, cancellationToken);
        if (user == null)
            return Result<bool>.Failure("User not found");

        try
        {
            user.UpdateProfile(request.FirstName, request.LastName);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (System.Exception ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
    }
}
