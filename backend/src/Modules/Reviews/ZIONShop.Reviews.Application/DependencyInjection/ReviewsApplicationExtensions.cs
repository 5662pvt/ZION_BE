using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Reviews.Application.DependencyInjection;

public static class ReviewsApplicationExtensions
{
    public static IServiceCollection AddReviewsApplication(this IServiceCollection services)
    {
        // TODO: register MediatR/Validators/AutoMapper when this module is implemented.
        return services;
    }
}
