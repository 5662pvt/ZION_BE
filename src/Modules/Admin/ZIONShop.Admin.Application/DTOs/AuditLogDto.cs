namespace ZIONShop.Admin.Application.DTOs;

public record AuditLogDto(
    Guid Id,
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
