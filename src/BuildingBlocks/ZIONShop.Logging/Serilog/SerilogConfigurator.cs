using Microsoft.AspNetCore.Builder;
using Serilog;

namespace ZIONShop.Logging.Serilog;

public static class SerilogConfigurator
{
    public static WebApplicationBuilder UseZionSerilog(this WebApplicationBuilder builder)
    {
        // Console sink comes from the "Serilog:WriteTo" config section (appsettings*.json) -- do
        // not also add .WriteTo.Console() here, or every line is written twice.
        builder.Host.UseSerilog((ctx, services, cfg) => cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
        );
        return builder;
    }
}
