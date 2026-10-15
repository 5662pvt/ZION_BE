using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Cart.Application.IntegrationEvents;
using ZIONShop.Cart.Application.Interfaces;
using ZIONShop.Cart.Domain.Repositories;
using ZIONShop.Cart.Infrastructure.Persistence;
using ZIONShop.Cart.Infrastructure.Readers;
using ZIONShop.Cart.Infrastructure.Repositories;
using ZIONShop.Contracts.Cart;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.EventBus.Inbox;

namespace ZIONShop.Cart.Infrastructure.DependencyInjection;

public static class CartInfrastructureExtensions
{
    public static IServiceCollection AddCartInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CartDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", CartDbContext.Schema)));

        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<ICartUnitOfWork>(sp => sp.GetRequiredService<CartDbContext>());
        services.AddScoped<ICartReader, CartReader>();

        services.AddInboxAdmin<CartDbContext>();

        services.AddIntegrationEventHandler<OrderCreatedIntegrationEvent, OrderCreatedClearCartHandler, CartDbContext>();

        return services;
    }
}
