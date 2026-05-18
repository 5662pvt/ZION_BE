using Microsoft.EntityFrameworkCore;
using ZIONShop.Auth.Infrastructure.Persistence;
using ZIONShop.Cart.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Users.Infrastructure.Persistence;

namespace ZIONShop.Api.Extensions;

public static class WebApplicationExtensions
{
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Migration");

        try
        {
            await sp.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
            await AuthDbSeeder.SeedAsync(sp.GetRequiredService<AuthDbContext>());

            await sp.GetRequiredService<UsersDbContext>().Database.MigrateAsync();

            await sp.GetRequiredService<ProductsDbContext>().Database.MigrateAsync();
            await ProductsDbSeeder.SeedAsync(sp.GetRequiredService<ProductsDbContext>());

            await sp.GetRequiredService<CartDbContext>().Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database migration/seed skipped: {Message}", ex.Message);
        }
    }
}
