using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Cart.Application.Interfaces;
using ZIONShop.Cart.Domain.Repositories;
using ZIONShop.Cart.Infrastructure.Persistence;
using ZIONShop.Cart.Infrastructure.Repositories;

namespace ZIONShop.Cart.Infrastructure.DependencyInjection;

public static class CartInfrastructureExtensions
{
    public static IServiceCollection AddCartInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CartDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", CartDbContext.Schema)));

        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<ICartUnitOfWork>(sp => sp.GetRequiredService<CartDbContext>());
        return services;
    }
}
