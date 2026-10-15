using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ZIONShop.EventBus.Diagnostics;
using ZIONShop.Logging.Resource;

namespace ZIONShop.Observability;

/// <summary>
/// Wires the OpenTelemetry SDK (traces + metrics) against the ActivitySource/Meter instruments
/// already emitted by EventBus/Caching (<see cref="MessagingDiagnostics"/> and "ZIONShop.Caching"),
/// plus ASP.NET Core/HttpClient/Npgsql auto-instrumentation. The TracerProvider/MeterProvider are
/// always registered -- this is what keeps TraceId/SpanId flowing into every log line via
/// Activity.Current, independent of whether anything is actually exported -- but the OTLP exporter
/// itself is only added when an endpoint is configured, and never in the Testing environment, so a
/// dev box with nothing configured behaves exactly as before (no network calls, no console spam).
/// Shared by every host (API, Worker) -- each passes its own <paramref name="serviceName"/>.
/// </summary>
public static class ObservabilityExtensions
{
    private static readonly string[] DefaultExcludedPathPrefixes = { "/health", "/healthz", "/swagger" };

    private static readonly string[] OtlpWellKnownPaths = { "/v1/traces", "/v1/metrics", "/v1/logs" };

    public static IServiceCollection AddZionObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string serviceName,
        params string[] additionalExcludedPathPrefixes)
    {
        // Must run before ZionResourceAttributes.ServiceName is read below, and before Serilog's
        // deferred OpenTelemetry sink configuration (UseZionSerilog, built later at Build()) reads it
        // too -- see ZionResourceAttributes.SetDefaultServiceName.
        ZionResourceAttributes.SetDefaultServiceName(serviceName);

        var otlpEndpointSetting = configuration["Otel:Endpoint"] ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var otlpEndpoint = string.IsNullOrWhiteSpace(otlpEndpointSetting) ? null : new Uri(otlpEndpointSetting);
        var exportOtlp = !environment.IsEnvironment("Testing") && otlpEndpoint is not null;

        var resourceServiceName = ZionResourceAttributes.ServiceName;
        var serviceVersion = ZionResourceAttributes.ServiceVersion;
        var samplingRatio = configuration.GetValue<double?>("Otel:SamplingRatio") ?? 1.0;
        var excludedPathPrefixes = DefaultExcludedPathPrefixes.Concat(additionalExcludedPathPrefixes).ToArray();

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(resourceServiceName, serviceVersion: serviceVersion, serviceInstanceId: ZionResourceAttributes.ServiceInstanceId)
                .AddAttributes(new[] { new KeyValuePair<string, object>("deployment.environment", environment.EnvironmentName) }))
            .WithTracing(tracing => tracing.ConfigureTracePipeline(samplingRatio, otlpEndpoint, excludedPathPrefixes,
                addExporter: exportOtlp ? tracingBuilder => tracingBuilder.AddOtlpExporter(o => o.Endpoint = otlpEndpoint!) : null))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(MessagingDiagnostics.SourceName)
                    .AddMeter("ZIONShop.Caching");

                if (exportOtlp)
                    metrics.AddOtlpExporter(o => o.Endpoint = otlpEndpoint!);
            });

        return services;
    }

    /// <summary>
    /// Builds the trace pipeline -- sampler, instrumentation, then (when <paramref name="addExporter"/>
    /// is non-null) the <see cref="NpgsqlRootDropProcessor"/> followed by whatever it wires up. Factored
    /// out of <see cref="AddZionObservability"/> so integration tests can build the exact same pipeline
    /// against an <c>InMemoryExporter</c> instead of OTLP and catch wiring regressions (dropped
    /// activities, missing Resource, un-filtered OTLP export calls) instead of only noticing them in
    /// the Aspire Dashboard. <paramref name="addExporter"/> is null when nothing will ever be exported
    /// (no endpoint configured, or the Testing environment) -- in that case there's no exporter
    /// processor for <see cref="NpgsqlRootDropProcessor"/> to protect, so it is skipped too.
    /// <paramref name="otlpEndpoint"/> is used only to recognize the exporter's own outgoing HTTP calls
    /// in the HttpClient instrumentation filter below -- it does not add the exporter itself.
    /// <paramref name="excludedPathPrefixes"/> defaults to health/swagger when omitted (e.g. from a test
    /// building a production-like pipeline that never serves those paths anyway).
    /// </summary>
    public static TracerProviderBuilder ConfigureTracePipeline(
        this TracerProviderBuilder tracing, double samplingRatio, Uri? otlpEndpoint,
        IReadOnlyCollection<string>? excludedPathPrefixes = null, Action<TracerProviderBuilder>? addExporter = null)
    {
        var effectiveExcludedPathPrefixes = excludedPathPrefixes ?? DefaultExcludedPathPrefixes;

        tracing
            .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
            .AddSource(MessagingDiagnostics.SourceName)
            .AddSource("ZIONShop.*")
            .AddAspNetCoreInstrumentation(o =>
            {
                o.RecordException = true;
                o.Filter = httpContext => !IsExcludedPath(httpContext.Request.Path, effectiveExcludedPathPrefixes);
                o.EnrichWithHttpResponse = (activity, response) =>
                {
                    var userId = response.HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                        ?? response.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userId))
                        activity.SetTag("enduser.id", userId);
                };
            })
            // The OTLP exporter's own export calls go out over HttpClient, which this same
            // instrumentation would otherwise record as a "POST" span with no route every export
            // interval -- filter those out before they're even sampled.
            .AddHttpClientInstrumentation(o => o.FilterHttpRequestMessage =
                request => !IsOtlpExportRequest(request.RequestUri, otlpEndpoint))
            .AddNpgsql();

        if (addExporter is not null)
        {
            // Must run before the exporter's own processor (added below via addExporter): processors
            // run in registration order, and clearing Recorded here keeps root Npgsql activities out
            // of every exporter without ever wrapping -- and thereby hiding the Resource from -- the
            // exporter's processor.
            tracing.AddProcessor(new NpgsqlRootDropProcessor());
            addExporter(tracing);
        }

        return tracing;
    }

    public static bool IsOtlpExportRequest(Uri? requestUri, Uri? otlpEndpoint)
    {
        if (requestUri is null)
            return false;

        if (otlpEndpoint is not null
            && string.Equals(requestUri.Host, otlpEndpoint.Host, StringComparison.OrdinalIgnoreCase)
            && requestUri.Port == otlpEndpoint.Port)
            return true;

        if (requestUri.AbsolutePath.StartsWith("/opentelemetry.proto.collector.", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var wellKnownPath in OtlpWellKnownPaths)
            if (requestUri.AbsolutePath.Equals(wellKnownPath, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    private static bool IsExcludedPath(PathString path, IReadOnlyCollection<string> excludedPathPrefixes)
    {
        foreach (var prefix in excludedPathPrefixes)
            if (path.StartsWithSegments(prefix))
                return true;
        return false;
    }
}
