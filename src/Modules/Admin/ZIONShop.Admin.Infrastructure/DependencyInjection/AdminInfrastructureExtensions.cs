using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Admin.Domain.Repositories;
using ZIONShop.Admin.Infrastructure.Persistence;
using ZIONShop.Admin.Infrastructure.Repositories;
using ZIONShop.Admin.Infrastructure.Services;
using ZIONShop.Contracts.Admin;

namespace ZIONShop.Admin.Infrastructure.DependencyInjection;

public static class AdminInfrastructureExtensions
{
    public static IServiceCollection AddAdminInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AdminDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AdminDbContext.Schema)));

        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();

        return services;
    }
}
