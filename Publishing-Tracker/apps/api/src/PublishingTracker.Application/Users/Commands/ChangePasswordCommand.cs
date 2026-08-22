using MediatR;

using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Common.Interfaces;
using BCrypt.Net;

namespace PublishingTracker.Application.Users.Commands;

public record ChangePasswordCommand(string OldPassword, string NewPassword) : IRequest<Result<bool>>;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly IQueryExecutor _queries;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;

    public ChangePasswordCommandHandler(IApplicationDbContext context, IQueryExecutor queries, ICurrentUserService currentUserService, IEmailService emailService)
    {
        _context = context;
        _queries = queries;
        _currentUserService = currentUserService;
        _emailService = emailService;
    }

    public async Task<Result<bool>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Result<bool>.Failure("Unauthorized");

        var user = await _queries.FirstOrDefaultAsync(_context.Users.Where(u => u.Id == userId.Value), cancellationToken);
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
            
            await _emailService.SendEmailAsync(
                user.Email, 
                "Password Changed", 
                "Your password has been successfully changed. If this was not you, please contact support immediately."
            );
            
            return Result<bool>.Success(true);
        }
        catch (System.Exception ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
    }
}
