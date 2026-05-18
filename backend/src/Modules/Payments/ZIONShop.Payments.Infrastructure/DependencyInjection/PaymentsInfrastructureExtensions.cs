using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Payments.Infrastructure.DependencyInjection;

public static class PaymentsInfrastructureExtensions
{
    public static IServiceCollection AddPaymentsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // TODO: register DbContext and repositories when this module is implemented.
        return services;
    }
}
