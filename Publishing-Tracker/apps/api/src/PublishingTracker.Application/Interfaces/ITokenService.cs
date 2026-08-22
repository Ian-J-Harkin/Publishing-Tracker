using PublishingTracker.Domain.Entities;

namespace PublishingTracker.Application.Interfaces;

public interface ITokenService
{
    string CreateToken(User user);
}
