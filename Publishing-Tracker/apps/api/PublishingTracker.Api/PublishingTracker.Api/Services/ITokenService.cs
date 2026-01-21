using PublishingTracker.Api.Models;

namespace PublishingTracker.Api.Services
{
    public interface ITokenService
    {
        string CreateToken(User user);
    }
}