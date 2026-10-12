using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Reviews.Application.Services;

namespace ZIONShop.Reviews.Application.DependencyInjection;

public static class ReviewsApplicationExtensions
{
    public static IServiceCollection AddReviewsApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddScoped<ProductRatingSummaryUpdater>();
        return services;
    }
}
