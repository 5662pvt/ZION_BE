using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ZIONShop.HealthChecks;

public record SystemHealthCheckDto(string Name, string Status, double DurationMs, string? Description);

public static class HealthReportMapper
{
    public static IReadOnlyList<SystemHealthCheckDto> ToChecks(this HealthReport report) =>
        report.Entries
            .Select(e => new SystemHealthCheckDto(e.Key, e.Value.Status.ToString(), e.Value.Duration.TotalMilliseconds, e.Value.Description))
            .ToList();
}

/// <summary>Shared JSON body for /health/live and /health/ready -- never includes connection
/// strings or exception detail, only the safe, fixed <see cref="IHealthCheck"/> descriptions set
/// by each check in this project.</summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.ToChecks()
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
