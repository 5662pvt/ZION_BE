using Microsoft.EntityFrameworkCore;
using ZIONShop.Auth.Infrastructure.Persistence;
using ZIONShop.Contracts.Auth;

namespace ZIONShop.Auth.Infrastructure.Readers;

public class UserContactReader : IUserContactReader
{
    private readonly AuthDbContext _db;

    public UserContactReader(AuthDbContext db) => _db = db;

    public async Task<UserContact?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        return user is null ? null : new UserContact(user.Id, user.Email, user.DisplayName);
    }
}
