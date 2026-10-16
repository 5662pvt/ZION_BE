using ZIONShop.Admin.Domain.Entities;

namespace ZIONShop.Admin.Domain.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken cancellationToken = default);
    IQueryable<AuditLog> Query();
}
