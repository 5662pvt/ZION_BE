using ZIONShop.Admin.Domain.Entities;
using ZIONShop.Admin.Infrastructure.Persistence;
using ZIONShop.Contracts.Admin;

namespace ZIONShop.Admin.Infrastructure.Services;

/// <summary>
/// Writes directly against <see cref="AdminDbContext"/> and saves immediately — called from the API's audit
/// action filter after the main request's own unit of work has already committed, so it owns its own save.
/// </summary>
public class AuditLogWriter : IAuditLogWriter
{
    private readonly AdminDbContext _db;

    public AuditLogWriter(AdminDbContext db) => _db = db;

    public async Task WriteAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        var log = AuditLog.Create(
            entry.OccurredAt, entry.ActorId, entry.ActorEmail, entry.Action, entry.ResourceType, entry.ResourceId,
            entry.HttpMethod, entry.Path, entry.StatusCode, entry.IpAddress, entry.CorrelationId, entry.TraceId);

        await _db.AuditLogs.AddAsync(log, ct);
        await _db.SaveChangesAsync(ct);
    }
}
