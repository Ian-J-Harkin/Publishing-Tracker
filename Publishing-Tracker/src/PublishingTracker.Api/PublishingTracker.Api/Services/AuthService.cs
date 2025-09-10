using Microsoft.EntityFrameworkCore;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;

namespace PublishingTracker.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly PublishingTrackerDbContext _context;
        private readonly ITokenService _tokenService;

        public AuthService(PublishingTrackerDbContext context, ITokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return null; // Email already exists
            }

            var user = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, BCrypt.Net.BCrypt.GenerateSalt(), enhancedEntropy: false, hashType: BCrypt.Net.HashType.SHA256)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = _tokenService.CreateToken(user);
            return new AuthResponse
            {
                Token = token,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
        }

        public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

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
}