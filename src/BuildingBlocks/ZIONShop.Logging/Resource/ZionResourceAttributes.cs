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
    private static string _defaultServiceName = "zionshop-api";

    // Computed per read (not cached in a static initializer like the other two properties below) so
    // that SetDefaultServiceName -- called by the hosting building block (ZIONShop.Observability)
    // before either this or the OpenTelemetry SDK resource is read -- takes effect for both sides.
    public static string ServiceName =>
        Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? _defaultServiceName;

    public static string ServiceVersion { get; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    public static string ServiceInstanceId { get; } = Guid.NewGuid().ToString();

    /// <summary>Sets the fallback used when <c>OTEL_SERVICE_NAME</c> is not set (e.g. "zionshop-api"
    /// vs "zionshop-worker") -- the env var always wins regardless. Must run before Serilog's
    /// deferred OpenTelemetry sink configuration (<c>UseZionSerilog</c>) actually builds, which
    /// happens at <c>WebApplication.Build()</c>/<c>Host.Build()</c>, so calling this from
    /// AddZionObservability (always invoked earlier, during service registration) is safe.</summary>
    public static void SetDefaultServiceName(string serviceName)
    {
        if (!string.IsNullOrWhiteSpace(serviceName))
            _defaultServiceName = serviceName;
    }
}
