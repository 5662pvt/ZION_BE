namespace ZIONShop.Admin.Domain.Entities;

/// <summary>Immutable record of a successful admin/staff mutation, written by the API's audit action filter.</summary>
public class AuditLog
{
    private AuditLog() { }

    public Guid Id { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public Guid? ActorId { get; private set; }
    public string? ActorEmail { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public string? ResourceId { get; private set; }
    public string HttpMethod { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public string? IpAddress { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? TraceId { get; private set; }

    public static AuditLog Create(
        DateTime occurredAt, Guid? actorId, string? actorEmail, string action, string resourceType, string? resourceId,
        string httpMethod, string path, int statusCode, string? ipAddress, string? correlationId, string? traceId) =>
        new()
        {
            Id = Guid.NewGuid(),
            OccurredAt = occurredAt,
            ActorId = actorId,
            ActorEmail = actorEmail,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            HttpMethod = httpMethod,
            Path = path,
            StatusCode = statusCode,
            IpAddress = ipAddress,
            CorrelationId = correlationId,
            TraceId = traceId
        };
}
