using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Promotions.Application.DependencyInjection;

public static class PromotionsApplicationExtensions
{
    public static IServiceCollection AddPromotionsApplication(this IServiceCollection services)
    {
        // TODO: register MediatR/Validators/AutoMapper when this module is implemented.
        return services;
    }
}
