using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.Auth.Infrastructure.Persistence;
using ZIONShop.Auth.Infrastructure.Repositories;

namespace ZIONShop.Auth.Infrastructure.DependencyInjection;

public static class AuthInfrastructureExtensions
{
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuthDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AuthDbContext.Schema)));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthUnitOfWork>(sp => sp.GetRequiredService<AuthDbContext>());
        return services;
    }
}
