using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Contracts.Auth;
using ZIONShop.Contracts.Users;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.EventBus.Inbox;
using ZIONShop.Users.Application.IntegrationEventHandlers;
using ZIONShop.Users.Application.Interfaces;
using ZIONShop.Users.Domain.Repositories;
using ZIONShop.Users.Infrastructure.Persistence;
using ZIONShop.Users.Infrastructure.Readers;
using ZIONShop.Users.Infrastructure.Repositories;

namespace ZIONShop.Users.Infrastructure.DependencyInjection;

public static class UsersInfrastructureExtensions
{
    public static IServiceCollection AddUsersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", UsersDbContext.Schema)));

        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IWishlistRepository, WishlistRepository>();
        services.AddScoped<IUsersUnitOfWork>(sp => sp.GetRequiredService<UsersDbContext>());
        services.AddScoped<IUserAddressReader, UserAddressReader>();
        services.AddScoped<IUserProfileReader, UserProfileReader>();

        services.AddInboxAdmin<UsersDbContext>();

        services.AddIntegrationEventHandler<UserRegisteredIntegrationEvent, UserRegisteredIntegrationEventHandler, UsersDbContext>();

        return services;
    }
}
