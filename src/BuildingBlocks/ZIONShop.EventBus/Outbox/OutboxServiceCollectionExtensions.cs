using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.EventBus.Outbox;

public static class OutboxServiceCollectionExtensions
{
    public static IServiceCollection AddOutbox<TDbContext>(this IServiceCollection services) where TDbContext : DbContext
    {
        services.AddScoped<IOutbox<TDbContext>, Outbox<TDbContext>>();
        services.AddHostedService<OutboxPublisherService<TDbContext>>();
        return services;
    }
}
