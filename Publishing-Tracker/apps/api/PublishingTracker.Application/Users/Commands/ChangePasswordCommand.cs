using MediatR;

using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Common.Interfaces;
using System.Threading;
using System.Threading.Tasks;
using BCrypt.Net;

namespace PublishingTracker.Application.Users.Commands;

public record ChangePasswordCommand(string OldPassword, string NewPassword) : IRequest<Result<bool>>;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ChangePasswordCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Result<bool>.Failure("Unauthorized");

        var user = await _context.Users.FindAsync(new object[] { userId.Value }, cancellationToken);
        if (user == null)
            return Result<bool>.Failure("User not found");

        // Verify old password
        if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash, false, BCrypt.Net.HashType.SHA256))
        {
            return Result<bool>.Failure("Incorrect old password");
        }

        // Hash new password
        var salt = BCrypt.Net.BCrypt.GenerateSalt();
        var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, salt, false, BCrypt.Net.HashType.SHA256);

        try
        {
            user.ChangePassword(newHash);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch (System.Exception ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
    }
}
