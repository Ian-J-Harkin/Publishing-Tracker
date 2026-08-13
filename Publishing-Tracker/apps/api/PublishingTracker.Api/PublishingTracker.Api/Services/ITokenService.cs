using PublishingTracker.Api.Domain.Entities;

namespace PublishingTracker.Api.Services
{
    public interface ITokenService
    {
        string CreateToken(User user);
    }
}