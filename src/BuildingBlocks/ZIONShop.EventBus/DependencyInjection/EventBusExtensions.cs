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

        // Ensures the registry exists even if no module has registered a handler yet via
        // AddIntegrationEventHandler<,>(), so the RabbitMQ consumer can always resolve it.
        IntegrationEventHandlerExtensions.GetOrCreateRegistry(services);

        if (string.Equals(provider, "RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<RabbitMqConnection>();
            services.AddSingleton<RabbitMqPublisher>();
            services.AddSingleton<IEventBus, RabbitMqEventBus>();
            services.AddSingleton<IDeadLetterAdmin, RabbitMqDeadLetterAdmin>();
            services.AddZionBackgroundService<RabbitMqConsumerService>(configuration);
        }
        else
        {
            services.AddSingleton<IEventBus, InMemoryEventBus>();
            services.AddSingleton<IDeadLetterAdmin, InMemoryDeadLetterAdmin>();
        }
        return services;
    }
}
