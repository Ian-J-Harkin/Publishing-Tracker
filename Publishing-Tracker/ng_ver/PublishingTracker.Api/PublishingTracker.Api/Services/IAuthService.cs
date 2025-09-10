using PublishingTracker.Api.Models.Dtos;

namespace PublishingTracker.Api.Services
{
    public interface IAuthService
    {
        Task<AuthResponse?> RegisterAsync(RegisterRequest request);
        Task<AuthResponse?> LoginAsync(LoginRequest request);
    }
}