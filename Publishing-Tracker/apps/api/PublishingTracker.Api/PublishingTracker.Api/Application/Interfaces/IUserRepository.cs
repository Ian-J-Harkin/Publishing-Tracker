using PublishingTracker.Api.Domain.Entities;
using PublishingTracker.Domain.Entities;

namespace PublishingTracker.Application.Interfaces;
   
public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByIdAsync(int id, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    Task UpdateAsync(User user, CancellationToken ct);
}


