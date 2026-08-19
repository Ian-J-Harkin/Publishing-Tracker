using MediatR;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Common.Interfaces;

using PublishingTracker.Domain.Dtos;

namespace PublishingTracker.Application.Auth.Queries;

public record LoginUserQuery(string Email, string Password) : IRequest<AuthResponse?>;

public class LoginUserQueryHandler : IRequestHandler<LoginUserQuery, AuthResponse?>
{
    private readonly IApplicationDbContext _context;
    private readonly ITokenService _tokenService;

    public LoginUserQueryHandler(IApplicationDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse?> Handle(LoginUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash, hashType: BCrypt.Net.HashType.SHA256))
        {
            return null; // Invalid credentials
        }

        var token = _tokenService.CreateToken(user);
        return new AuthResponse
        {
            Token = token,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName
        };
    }
}
