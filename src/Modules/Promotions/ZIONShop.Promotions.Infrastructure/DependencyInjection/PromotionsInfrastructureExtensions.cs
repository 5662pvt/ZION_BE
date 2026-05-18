using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Promotions.Infrastructure.DependencyInjection;

public static class PromotionsInfrastructureExtensions
{
    public static IServiceCollection AddPromotionsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // TODO: register DbContext and repositories when this module is implemented.
        return services;
    }
}
