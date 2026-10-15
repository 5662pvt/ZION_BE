using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ZIONShop.EventBus.DependencyInjection;

/// <summary>
/// Binds the "BackgroundProcessing" config section. Defaults to enabled so the single-process dev
/// run (API hosting every consumer/outbox/background job itself) behaves exactly as before this
/// option existed -- only a host that explicitly sets <c>BackgroundProcessing:Enabled=false</c> (the
/// API, once a separate Worker host takes over those jobs) opts out.
/// </summary>
public class BackgroundProcessingOptions
{
    public const string SectionName = "BackgroundProcessing";

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Single gate for every consumer/outbox/background-job <see cref="IHostedService"/> registration
/// (RabbitMqConsumerService, OutboxPublisherService&lt;TDbContext&gt;, ReservationExpiryBackgroundService,
/// StaleReservationReleaseBackgroundService, IdempotencyKeyCleanupBackgroundService) so a host can be
/// told "don't run any background job" (the API, once ZIONShop.Worker owns them) without touching each
/// registration site's own logic.
/// </summary>
public static class BackgroundProcessingExtensions
{
    public static bool IsBackgroundProcessingEnabled(this IConfiguration configuration) =>
        configuration.GetSection(BackgroundProcessingOptions.SectionName).Get<BackgroundProcessingOptions>()?.Enabled
        ?? true;

    public static IServiceCollection AddZionBackgroundService<THostedService>(
        this IServiceCollection services, IConfiguration configuration)
        where THostedService : class, IHostedService
    {
        if (configuration.IsBackgroundProcessingEnabled())
            services.AddHostedService<THostedService>();
        return services;
    }

    public static IServiceCollection AddZionBackgroundService<THostedService>(
        this IServiceCollection services, IConfiguration configuration, Func<IServiceProvider, THostedService> implementationFactory)
        where THostedService : class, IHostedService
    {
        if (configuration.IsBackgroundProcessingEnabled())
            services.AddHostedService(implementationFactory);
        return services;
    }
}
