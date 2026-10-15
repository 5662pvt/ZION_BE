using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace ZIONShop.Api.IntegrationTests.Infrastructure;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("zionshop_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string UploadsRootPath { get; } =
        Path.Combine(Path.GetTempPath(), "zion-uploads-" + Guid.NewGuid().ToString("N"));

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();
        Directory.CreateDirectory(UploadsRootPath);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        if (Directory.Exists(UploadsRootPath))
            Directory.Delete(UploadsRootPath, recursive: true);
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Program.cs reads some of these eagerly from builder.Configuration during service
        // registration -- e.g. AuthInfrastructureExtensions checks Email:Host to decide between
        // SmtpEmailSender/LoggingEmailSender -- before WebApplicationFactory's ConfigureWebHost
        // hook runs. ConfigureAppConfiguration overrides only apply once builder.Build() is
        // intercepted, which is too late for those reads, so set real process environment
        // variables instead: WebApplication.CreateBuilder(args) picks these up immediately.
        // _postgres is already started by the time the first test request builds the host
        // (xunit calls IAsyncLifetime.InitializeAsync before any test in the collection runs).
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("EventBus__Provider", "InMemory");
        // appsettings.Development.json already has Email:Host = "" (LoggingEmailSender),
        // so no override is needed here.
        // Dev appsettings set an absolute PublicBaseUrl (http://localhost:8080). On Windows,
        // SetEnvironmentVariable with an empty string REMOVES the variable rather than setting
        // it, so an empty override here does not actually override appsettings -- it leaves
        // http://localhost:8080 in effect. Use a non-empty sentinel instead; tests assert on
        // the path portion of the returned URL rather than assuming it's relative.
        Environment.SetEnvironmentVariable("ImageStorage__PublicBaseUrl", "http://test.local");
        Environment.SetEnvironmentVariable("ImageStorage__LocalRootPath", UploadsRootPath);

        // RateLimiting:* is bound into IOptions<RateLimitingOptions> and only read from DI inside
        // the limiter partition factories at request time -- well after builder.Build() -- so,
        // unlike the settings above, a plain ConfigureAppConfiguration override here is seen
        // correctly. The suite logs in/registers/searches/checks out far more than the production
        // defaults allow, so raise all three here. RateLimitingTests.cs layers its own low-limit
        // ConfigureAppConfiguration on top via WithWebHostBuilder; being registered after this one,
        // it wins for the keys it sets (last-registered configuration source wins).
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:AuthPermitLimit"] = "1000",
            ["RateLimiting:CheckoutPermitLimit"] = "1000",
            ["RateLimiting:SearchPermitLimit"] = "1000",
        }));
    }
}
