using PublishingTracker.Api.Domain.Entities;

namespace PublishingTracker.Api.Application.Interfaces
{
    public interface IJwtGenerator
    {
        string CreateToken(User user);
        // Optional: Validate/Parse token if you want to move that out of the controller as well.
    }
}
