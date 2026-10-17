using Microsoft.EntityFrameworkCore;
using ZIONShop.Auth.Infrastructure.Persistence;
using ZIONShop.Contracts.Auth;

namespace ZIONShop.Auth.Infrastructure.Readers;

public class StaffUserReader : IStaffUserReader
{
    private readonly AuthDbContext _db;

    public StaffUserReader(AuthDbContext db) => _db = db;

    public async Task<IReadOnlyList<StaffUser>> GetAdminAndStaffUserIdsAsync(CancellationToken ct = default)
    {
        var ids = await _db.Users.AsNoTracking()
            .Where(u => u.RolesRaw.ToUpper().Contains("ADMIN") || u.RolesRaw.ToUpper().Contains("STAFF"))
            .Select(u => u.Id)
            .Distinct()
            .ToListAsync(ct);

        return ids.Select(id => new StaffUser(id)).ToList();
    }
}
