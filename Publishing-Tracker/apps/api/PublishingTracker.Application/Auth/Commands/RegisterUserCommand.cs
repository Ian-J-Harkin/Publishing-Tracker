using MediatR;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Domain.Entities;

using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Domain;

namespace PublishingTracker.Application.Auth.Commands;

public record RegisterUserCommand(string Email, string Password, string FirstName, string LastName) : IRequest<Result<AuthResponse>>;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<AuthResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IQueryExecutor _queries;
    private readonly ITokenService _tokenService;

    public RegisterUserCommandHandler(IApplicationDbContext context, IQueryExecutor queries, ITokenService tokenService)
    {
        _context = context;
        _queries = queries;
        _tokenService = tokenService;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        if (await _queries.AnyAsync(_context.Users.Where(u => u.Email == request.Email), cancellationToken))
        {
            return Result<AuthResponse>.Failure("Email already exists.");
        }

        // First user ever registered becomes Admin; all others default to User.
        var isFirstUser = !await _queries.AnyAsync(_context.Users, cancellationToken);
        var role = isFirstUser ? "Admin" : "User";

        var user = User.Create(
            request.Email,
            BCrypt.Net.BCrypt.HashPassword(request.Password, BCrypt.Net.BCrypt.GenerateSalt(), enhancedEntropy: false, hashType: BCrypt.Net.HashType.SHA256),
            request.FirstName,
            request.LastName,
            role
        );

        _context.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        var token = _tokenService.CreateToken(user);
        return Result<AuthResponse>.Success(new AuthResponse
        {
            Token = token,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName
        });
    }
}
