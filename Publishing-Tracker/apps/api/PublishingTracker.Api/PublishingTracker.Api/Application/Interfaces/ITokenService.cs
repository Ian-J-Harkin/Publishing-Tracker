using PublishingTracker.Api.Domain.Entities;

namespace PublishingTracker.Application.Interfaces;

public interface ITokenService
{
    string CreateToken(User user);
}
