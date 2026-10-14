using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZIONShop.Payments.Application.Services;

namespace ZIONShop.Payments.Application.DependencyInjection;

public static class PaymentsApplicationExtensions
{
    public static IServiceCollection AddPaymentsApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<FakeWebhookProcessor>();

        // Integration event handlers are registered in PaymentsInfrastructureExtensions, since the
        // inbox-idempotent registration overload needs PaymentsDbContext (an Infrastructure type).

        return services;
    }
}
