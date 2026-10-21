using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ZIONShop.Inventory.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef migrations add` — avoids building the full app host (and its module DI graph) just to generate a migration.</summary>
public class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<InventoryDbContext>();
        builder.UseNpgsql("Host=localhost;Database=ZionShop_designtime",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.Schema));
        return new InventoryDbContext(builder.Options);
    }
}
