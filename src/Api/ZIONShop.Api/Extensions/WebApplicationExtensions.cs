using Microsoft.EntityFrameworkCore;
using ZIONShop.Admin.Infrastructure.Persistence;
using ZIONShop.Auth.Infrastructure.Persistence;
using ZIONShop.Cart.Infrastructure.Persistence;
using ZIONShop.Contracts.Products;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Loyalty.Infrastructure.Persistence;
using ZIONShop.Notifications.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Payments.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Reviews.Infrastructure.Persistence;
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
            var loadTestCustomers = app.Configuration.GetValue<int>("Seed:LoadTestCustomers");
            await AuthDbSeeder.SeedAsync(sp.GetRequiredService<AuthDbContext>(), loadTestCustomers);

            await sp.GetRequiredService<UsersDbContext>().Database.MigrateAsync();
            await AdministrativeUnitsSeeder.SeedAsync(sp.GetRequiredService<UsersDbContext>());

            await sp.GetRequiredService<ProductsDbContext>().Database.MigrateAsync();
            await ProductsDbSeeder.SeedAsync(sp.GetRequiredService<ProductsDbContext>());

            await sp.GetRequiredService<CartDbContext>().Database.MigrateAsync();

            await sp.GetRequiredService<OrdersDbContext>().Database.MigrateAsync();

            await sp.GetRequiredService<InventoryDbContext>().Database.MigrateAsync();
            await InventoryDbSeeder.SeedAsync(sp.GetRequiredService<InventoryDbContext>(), sp.GetRequiredService<IProductCatalogReader>());

            await sp.GetRequiredService<PaymentsDbContext>().Database.MigrateAsync();

            await sp.GetRequiredService<PromotionsDbContext>().Database.MigrateAsync();

            await sp.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();

            await sp.GetRequiredService<ReviewsDbContext>().Database.MigrateAsync();

            await sp.GetRequiredService<AdminDbContext>().Database.MigrateAsync();

            await sp.GetRequiredService<LoyaltyDbContext>().Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database migration/seed skipped: {Message}", ex.Message);
        }
    }
}
