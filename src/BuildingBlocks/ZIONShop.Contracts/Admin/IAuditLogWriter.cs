namespace ZIONShop.Contracts.Admin;

public sealed record AuditLogEntry(
    DateTime OccurredAt,
    Guid? ActorId,
    string? ActorEmail,
    string Action,
    string ResourceType,
    string? ResourceId,
    string HttpMethod,
    string Path,
    int StatusCode,
    string? IpAddress,
    string? CorrelationId,
    string? TraceId);

public interface IAuditLogWriter
{
    Task WriteAsync(AuditLogEntry entry, CancellationToken ct = default);
}
