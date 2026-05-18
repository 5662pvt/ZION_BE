using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Payments.Application.DependencyInjection;

public static class PaymentsApplicationExtensions
{
    public static IServiceCollection AddPaymentsApplication(this IServiceCollection services)
    {
        // TODO: register MediatR/Validators/AutoMapper when this module is implemented.
        return services;
    }
}
