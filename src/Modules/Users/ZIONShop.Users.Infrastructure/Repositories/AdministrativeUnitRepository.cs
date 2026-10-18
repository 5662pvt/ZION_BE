using Microsoft.EntityFrameworkCore;
using ZIONShop.Users.Domain.Entities;
using ZIONShop.Users.Domain.Repositories;
using ZIONShop.Users.Infrastructure.Persistence;

namespace ZIONShop.Users.Infrastructure.Repositories;

public class AdministrativeUnitRepository : IAdministrativeUnitRepository
{
    private readonly UsersDbContext _db;

    public AdministrativeUnitRepository(UsersDbContext db) => _db = db;

    public async Task<IReadOnlyList<Province>> GetProvincesAsync(CancellationToken cancellationToken = default) =>
        await _db.Provinces.AsNoTracking().OrderBy(p => p.Name).ToListAsync(cancellationToken);

    public Task<Province?> GetProvinceByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Provinces.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Province?> FindProvinceByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLower();
        return _db.Provinces.AsNoTracking().FirstOrDefaultAsync(p => p.Name.ToLower() == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<Ward>> GetWardsByProvinceIdAsync(Guid provinceId, CancellationToken cancellationToken = default) =>
        await _db.Wards.AsNoTracking().Where(w => w.ProvinceId == provinceId).OrderBy(w => w.Name).ToListAsync(cancellationToken);

    public Task<Ward?> FindWardByNameAsync(Guid provinceId, string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLower();
        return _db.Wards.AsNoTracking().FirstOrDefaultAsync(w => w.ProvinceId == provinceId && w.Name.ToLower() == normalized, cancellationToken);
    }
}
