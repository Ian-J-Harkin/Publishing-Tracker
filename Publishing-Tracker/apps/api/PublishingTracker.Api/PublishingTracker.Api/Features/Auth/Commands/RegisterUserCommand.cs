using MediatR;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Domain.Entities;
using PublishingTracker.Api.Models;
using PublishingTracker.Domain.Dtos;
using PublishingTracker.Api.Services;

namespace PublishingTracker.Api.Features.Auth.Commands;

public record RegisterUserCommand(string Email, string Password, string FirstName, string LastName) : IRequest<AuthResponse?>;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, AuthResponse?>
{
    private readonly PublishingTrackerDbContext _context;
    private readonly ITokenService _tokenService;

    public RegisterUserCommandHandler(PublishingTrackerDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse?> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email, cancellationToken))
        {
            return null; // Email already exists
        }

        var user = User.Create(
            request.Email,
            BCrypt.Net.BCrypt.HashPassword(request.Password, BCrypt.Net.BCrypt.GenerateSalt(), enhancedEntropy: false, hashType: BCrypt.Net.HashType.SHA256),
            request.FirstName,
            request.LastName
        );

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

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
