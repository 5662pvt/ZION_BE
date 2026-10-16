using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Users;
using ZIONShop.Users.Infrastructure.Persistence;

namespace ZIONShop.Users.Infrastructure.Readers;

public class UserProfileReader : IUserProfileReader
{
    private readonly UsersDbContext _db;

    public UserProfileReader(UsersDbContext db) => _db = db;

    public async Task<IReadOnlyList<UserProfileSnapshot>> GetProfilesAsync(IReadOnlyCollection<Guid> authUserIds, CancellationToken cancellationToken = default)
    {
        if (authUserIds.Count == 0) return Array.Empty<UserProfileSnapshot>();

        return await _db.UserProfiles.AsNoTracking()
            .Where(p => authUserIds.Contains(p.AuthUserId))
            .Select(p => new UserProfileSnapshot(p.AuthUserId, p.FullName, p.PhoneNumber, p.Addresses.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> FindAuthUserIdsByPhoneAsync(string phone, CancellationToken cancellationToken = default)
    {
        var keyword = phone.Trim();
        if (keyword.Length == 0) return Array.Empty<Guid>();

        return await _db.UserProfiles.AsNoTracking()
            .Where(p => p.PhoneNumber != null && EF.Functions.Like(p.PhoneNumber, $"%{keyword}%"))
            .Select(p => p.AuthUserId)
            .ToListAsync(cancellationToken);
    }
}
