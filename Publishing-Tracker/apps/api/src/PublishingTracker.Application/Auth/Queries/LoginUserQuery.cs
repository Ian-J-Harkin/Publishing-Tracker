using MediatR;
using PublishingTracker.Application.Common.Interfaces;

using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Domain;

namespace PublishingTracker.Application.Auth.Queries;

public record LoginUserQuery(string Email, string Password) : IRequest<Result<AuthResponse>>;

public class LoginUserQueryHandler : IRequestHandler<LoginUserQuery, Result<AuthResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IQueryExecutor _queries;
    private readonly ITokenService _tokenService;

    public LoginUserQueryHandler(IApplicationDbContext context, IQueryExecutor queries, ITokenService tokenService)
    {
        _context = context;
        _queries = queries;
        _tokenService = tokenService;
    }

    public async Task<Result<AuthResponse>> Handle(LoginUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _queries.FirstOrDefaultAsync(_context.Users.Where(u => u.Email == request.Email), cancellationToken);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash, hashType: BCrypt.Net.HashType.SHA256))
        {
            return Result<AuthResponse>.Failure("Invalid email or password.");
        }

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
