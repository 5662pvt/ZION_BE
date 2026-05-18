using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.InMemory;
using ZIONShop.EventBus.RabbitMq;

namespace ZIONShop.EventBus.DependencyInjection;

public static class EventBusExtensions
{
    public static IServiceCollection AddZionEventBus(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        var provider = configuration.GetSection(RabbitMqOptions.SectionName)["Provider"] ?? "InMemory";

        if (string.Equals(provider, "RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<RabbitMqConnection>();
            services.AddSingleton<IEventBus, RabbitMqEventBus>();
        }
        else
        {
            services.AddSingleton<IEventBus, InMemoryEventBus>();
        }
        return services;
    }
}
