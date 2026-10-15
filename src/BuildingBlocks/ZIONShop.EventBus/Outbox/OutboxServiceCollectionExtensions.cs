using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.EventBus.DependencyInjection;

namespace ZIONShop.EventBus.Outbox;

public static class OutboxServiceCollectionExtensions
{
    public static IServiceCollection AddOutbox<TDbContext>(this IServiceCollection services, IConfiguration configuration) where TDbContext : DbContext
    {
        services.AddScoped<IOutbox<TDbContext>, Outbox<TDbContext>>();
        services.AddScoped<IOutboxAdminStore, OutboxAdminStore<TDbContext>>();
        services.AddZionBackgroundService<OutboxPublisherService<TDbContext>>(configuration);

        return services;
    }
}
