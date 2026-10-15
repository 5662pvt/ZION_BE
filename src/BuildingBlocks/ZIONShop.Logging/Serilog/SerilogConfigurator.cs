using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;
using ZIONShop.Logging.Resource;

namespace ZIONShop.Logging.Serilog;

public static class SerilogConfigurator
{
    public static WebApplicationBuilder UseZionSerilog(this WebApplicationBuilder builder)
    {
        // Console sink comes from the "Serilog:WriteTo" config section (appsettings*.json) -- do
        // not also add .WriteTo.Console() here, or every line is written twice.
        builder.Host.UseSerilog((ctx, services, cfg) =>
        {
            cfg.ReadFrom.Configuration(ctx.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName();

            // Same on/off switch as the trace/metric OTLP exporters (ObservabilityExtensions): only
            // ship logs to the collector when an endpoint is configured, and never from the Testing
            // environment, so a dev box with nothing set behaves exactly as before (Console + Seq only).
            var otlpEndpoint = ctx.Configuration["Otel:Endpoint"] ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
            if (!ctx.HostingEnvironment.IsEnvironment("Testing") && !string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                // Same resource identity as the OpenTelemetry SDK's TracerProvider/MeterProvider
                // (ObservabilityExtensions.AddZionObservability) -- otherwise the Aspire Dashboard shows
                // this one process as separate resources for logs vs traces/metrics.
                cfg.WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = ZionResourceAttributes.ServiceName,
                        ["service.version"] = ZionResourceAttributes.ServiceVersion,
                        ["service.instance.id"] = ZionResourceAttributes.ServiceInstanceId,
                        ["deployment.environment"] = ctx.HostingEnvironment.EnvironmentName
                    };
                });
            }
        });
        return builder;
    }
}
