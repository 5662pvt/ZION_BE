using Microsoft.EntityFrameworkCore;
using ZIONShop.Users.Domain.Entities;
using ZIONShop.Users.Domain.Repositories;
using ZIONShop.Users.Infrastructure.Persistence;

namespace ZIONShop.Users.Infrastructure.Repositories;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly UsersDbContext _db;

    public UserProfileRepository(UsersDbContext db) => _db = db;

    public Task<UserProfile?> GetByAuthUserIdAsync(Guid authUserId, CancellationToken cancellationToken = default) =>
        _db.UserProfiles.Include(p => p.Addresses).FirstOrDefaultAsync(p => p.AuthUserId == authUserId, cancellationToken);

    public Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.UserProfiles.Include(p => p.Addresses).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> ExistsForAuthUserAsync(Guid authUserId, CancellationToken cancellationToken = default) =>
        _db.UserProfiles.AnyAsync(p => p.AuthUserId == authUserId, cancellationToken);

    public async Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default) =>
        await _db.UserProfiles.AddAsync(profile, cancellationToken);

    public async Task AddAddressAsync(Address address, CancellationToken cancellationToken = default) =>
        await _db.Addresses.AddAsync(address, cancellationToken);

    public void Update(UserProfile profile)
    {
        if (_db.Entry(profile).State == EntityState.Detached)
            _db.UserProfiles.Update(profile);
    }
}
