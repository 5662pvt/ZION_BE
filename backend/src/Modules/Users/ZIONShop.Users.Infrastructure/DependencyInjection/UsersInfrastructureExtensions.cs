using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Users.Application.Interfaces;
using ZIONShop.Users.Domain.Repositories;
using ZIONShop.Users.Infrastructure.Persistence;
using ZIONShop.Users.Infrastructure.Repositories;

namespace ZIONShop.Users.Infrastructure.DependencyInjection;

public static class UsersInfrastructureExtensions
{
    public static IServiceCollection AddUsersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<UsersDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", UsersDbContext.Schema)));

        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IUsersUnitOfWork>(sp => sp.GetRequiredService<UsersDbContext>());
        return services;
    }
}
