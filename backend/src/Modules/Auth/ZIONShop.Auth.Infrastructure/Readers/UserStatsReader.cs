using Microsoft.EntityFrameworkCore;
using ZIONShop.Auth.Infrastructure.Persistence;
using ZIONShop.Contracts.Auth;

namespace ZIONShop.Auth.Infrastructure.Readers;

public class UserStatsReader : IUserStatsReader
{
    private readonly AuthDbContext _db;

    public UserStatsReader(AuthDbContext db) => _db = db;

    public async Task<int> CountCustomersRegisteredAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var rolesRawInRange = await _db.Users.AsNoTracking()
            .Where(u => u.CreatedDate >= fromUtc && u.CreatedDate < toUtc)
            .Select(u => u.RolesRaw)
            .ToListAsync(ct);

        return rolesRawInRange.Count(raw =>
            raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains("Customer"));
    }
}
