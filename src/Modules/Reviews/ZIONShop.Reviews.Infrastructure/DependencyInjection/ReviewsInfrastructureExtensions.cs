using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Reviews.Infrastructure.DependencyInjection;

public static class ReviewsInfrastructureExtensions
{
    public static IServiceCollection AddReviewsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // TODO: register DbContext and repositories when this module is implemented.
        return services;
    }
}
