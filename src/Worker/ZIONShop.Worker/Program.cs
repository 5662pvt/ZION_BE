using MediatR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using ZIONShop.Admin.Application.DependencyInjection;
using ZIONShop.Admin.Infrastructure.DependencyInjection;
using ZIONShop.Loyalty.Application.DependencyInjection;
using ZIONShop.Loyalty.Infrastructure.DependencyInjection;
using ZIONShop.Chat.Application.DependencyInjection;
using ZIONShop.Chat.Infrastructure.DependencyInjection;
using ZIONShop.Auth.Application.DependencyInjection;
using ZIONShop.Auth.DependencyInjection;
using ZIONShop.Auth.Infrastructure.DependencyInjection;
using ZIONShop.Cart.Application.DependencyInjection;
using ZIONShop.Cart.Infrastructure.DependencyInjection;
using ZIONShop.Caching.DependencyInjection;
using ZIONShop.Common.Behaviors;
using ZIONShop.Common.Email;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.HealthChecks;
using ZIONShop.Inventory.Application.DependencyInjection;
using ZIONShop.Inventory.Infrastructure.DependencyInjection;
using ZIONShop.Logging.Serilog;
using ZIONShop.Notifications.Application.DependencyInjection;
using ZIONShop.Notifications.Infrastructure.DependencyInjection;
using ZIONShop.Observability;
using ZIONShop.Orders.Application.DependencyInjection;
using ZIONShop.Orders.Infrastructure.DependencyInjection;
using ZIONShop.Payments.Application.DependencyInjection;
using ZIONShop.Payments.Infrastructure.DependencyInjection;
using ZIONShop.Products.Application.DependencyInjection;
using ZIONShop.Products.Infrastructure.DependencyInjection;
using ZIONShop.Promotions.Application.DependencyInjection;
using ZIONShop.Promotions.Infrastructure.DependencyInjection;
using ZIONShop.Realtime.DependencyInjection;
using ZIONShop.Reviews.Application.DependencyInjection;
using ZIONShop.Reviews.Infrastructure.DependencyInjection;
using ZIONShop.Users.Application.DependencyInjection;
using ZIONShop.Users.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateSlimBuilder(args);

// CreateSlimBuilder trims default configuration sources for startup size -- add back json/env
// explicitly so appsettings*.json (local dev) and the double-underscore env vars docker-compose
// sets for the "api" service (same connection strings/EventBus/Redis/Email/OTEL settings) both work
// here unchanged.
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    // This host exists specifically to run every consumer/outbox/background job -- force it on
    // regardless of what BackgroundProcessing:Enabled is set to elsewhere (e.g. the API's env block
    // copied verbatim into this service in docker-compose).
    .AddInMemoryCollection(new Dictionary<string, string?> { ["BackgroundProcessing:Enabled"] = "true" });

builder.UseZionSerilog();

builder.Services.AddZionObservability(builder.Configuration, builder.Environment, "zionshop-worker");
builder.Services.AddZionEventBus(builder.Configuration);
builder.Services.AddZionEmail(builder.Configuration);
if (!builder.Environment.IsEnvironment("Testing"))
{
    try { builder.Services.AddZionCaching(builder.Configuration); }
    catch { /* Redis optional, same fail-open as the API */ }
}
// Publish side for real-time notifications must exist here too -- this is the process where the
// Notifications integration-event handlers actually run once BackgroundProcessing:Enabled=false on
// the Api hands them over (see docker-compose). Delivery to the browser still only happens from the
// Api host's NotificationRelayHostedService/SignalR hub.
try { builder.Services.AddZionRealtime(builder.Configuration); }
catch { /* Redis optional, same fail-open as the API */ }

// MediatR pipeline behaviors -- same three as the API (Program.cs), registered against the same
// shared assemblies, since every module's integration-event handlers run through MediatR here too.
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));

// AddZionAuth is registered even though this host serves no authenticated HTTP endpoints (only
// /health/*): Auth.Application's LoginCommandHandler/RefreshTokenCommandHandler/
// RevokeRefreshTokenCommandHandler/AuthTokenIssuer all depend on IJwtTokenService (and
// ICurrentUserService, which degrades to "no user" without an HttpContext -- see
// CurrentUserService), and this host registers every module's Application layer below, so those
// handlers are always in the container and must resolve under ValidateOnBuild/ValidateScopes
// (Development) exactly like they do on the API.
builder.Services.AddZionAuth(builder.Configuration);

// Every module's Application + Infrastructure, exactly like the API (src/Api/ZIONShop.Api/Program.cs)
// -- this is the host where their consumer/outbox/background-job IHostedServices actually run once
// BackgroundProcessing:Enabled=false on the API hands them over (forced true above).
builder.Services.AddAuthApplication();
builder.Services.AddAuthInfrastructure(builder.Configuration);
builder.Services.AddUsersApplication();
builder.Services.AddUsersInfrastructure(builder.Configuration);
builder.Services.AddProductsApplication();
builder.Services.AddProductsInfrastructure(builder.Configuration);
builder.Services.AddCartApplication();
builder.Services.AddCartInfrastructure(builder.Configuration);

builder.Services
    .AddOrdersApplication().AddOrdersInfrastructure(builder.Configuration)
    .AddInventoryApplication().AddInventoryInfrastructure(builder.Configuration)
    .AddPaymentsApplication().AddPaymentsInfrastructure(builder.Configuration)
    .AddPromotionsApplication().AddPromotionsInfrastructure(builder.Configuration)
    .AddNotificationsApplication().AddNotificationsInfrastructure(builder.Configuration)
    .AddReviewsApplication().AddReviewsInfrastructure(builder.Configuration)
    .AddAdminApplication().AddAdminInfrastructure(builder.Configuration)
    .AddLoyaltyApplication().AddLoyaltyInfrastructure(builder.Configuration)
    .AddChatApplication().AddChatInfrastructure(builder.Configuration);

// Health checks -- same checks/tags as the API (src/Api/ZIONShop.Api/Program.cs), reused from the
// shared ZIONShop.HealthChecks building block so both hosts report readiness identically.
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres", tags: new[] { "ready" })
    .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" })
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "ready" });

var app = builder.Build();

// No MigrateAndSeedAsync here -- the API owns migrations. This host must tolerate starting before
// migrations finish: readiness stays Unhealthy until Postgres is reachable (PostgresHealthCheck does
// not check for a specific schema/table), and every hosted service above already retries its own
// work on failure (see each BackgroundService's ExecuteAsync) exactly as it does today in the API.
app.UseSerilogRequestLogging();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    },
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});

app.Run();

public partial class Program { }
