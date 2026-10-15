using System.Reflection;

namespace ZIONShop.Logging.Resource;

/// <summary>
/// Single source of truth for the OpenTelemetry resource identity (service.name, service.version,
/// service.instance.id) shared between the SDK's TracerProvider/MeterProvider resource
/// (ObservabilityExtensions) and the Serilog OpenTelemetry sink (SerilogConfigurator). Without this,
/// each side picked its own service.instance.id and the Aspire Dashboard showed one running process
/// as three separate resources. ServiceInstanceId is generated once per process (static field).
/// </summary>
public static class ZionResourceAttributes
{
    public static string ServiceName { get; } =
        Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "zionshop-api";

    public static string ServiceVersion { get; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    public static string ServiceInstanceId { get; } = Guid.NewGuid().ToString();
}
