using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Reviews.Application.Interfaces;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.Reviews.Infrastructure.Persistence;
using ZIONShop.Reviews.Infrastructure.Repositories;

namespace ZIONShop.Reviews.Infrastructure.DependencyInjection;

public static class ReviewsInfrastructureExtensions
{
    public static IServiceCollection AddReviewsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ReviewsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ReviewsDbContext.Schema)));

        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IProductRatingSummaryRepository, ProductRatingSummaryRepository>();
        services.AddScoped<IReviewsUnitOfWork>(sp => sp.GetRequiredService<ReviewsDbContext>());

        return services;
    }
}
