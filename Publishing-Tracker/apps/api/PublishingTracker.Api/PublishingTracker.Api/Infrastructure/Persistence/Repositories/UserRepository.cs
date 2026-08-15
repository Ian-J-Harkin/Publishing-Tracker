using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Api.Domain.Entities;

using PublishingTracker.Infrastructure.Persistence;

namespace PublishingTracker.Infrastructure.Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
      //  Implement the IUserRepository interface, injecting the PublishingTrackerDbContext.
      private readonly PublishingTrackerDbContext _db;

        // implement the GetByEmailAsync method to retrieve a user by email.    
        public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
        {
            return await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        }

        public async Task<User?> GetByIdAsync(int id, CancellationToken ct)
        {
            return await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        }

        // add AddAsync method to add a new user to the database.
        public async Task AddAsync(User user, CancellationToken ct)
        {
            await _db.Users.AddAsync(user, ct);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(User user, CancellationToken ct)
        {
            _db.Users.Update(user);
            await _db.SaveChangesAsync(ct);
        }

        public UserRepository(PublishingTrackerDbContext db)
      {
          _db = db;
      } 
    }
}
