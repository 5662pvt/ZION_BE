using ZIONShop.Admin.Domain.Entities;
using ZIONShop.Admin.Domain.Repositories;
using ZIONShop.Admin.Infrastructure.Persistence;

namespace ZIONShop.Admin.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AdminDbContext _db;

    public AuditLogRepository(AdminDbContext db) => _db = db;

    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default) =>
        await _db.AuditLogs.AddAsync(log, cancellationToken);

    public IQueryable<AuditLog> Query() => _db.AuditLogs.AsQueryable();
}
