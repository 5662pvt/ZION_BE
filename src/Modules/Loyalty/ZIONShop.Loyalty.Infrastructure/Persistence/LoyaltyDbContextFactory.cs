using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ZIONShop.Loyalty.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef migrations add` — avoids building the full app host (and its module DI graph) just to generate a migration.</summary>
public class LoyaltyDbContextFactory : IDesignTimeDbContextFactory<LoyaltyDbContext>
{
    public LoyaltyDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<LoyaltyDbContext>();
        builder.UseNpgsql("Host=localhost;Database=ZionShop_designtime",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", LoyaltyDbContext.Schema));
        return new LoyaltyDbContext(builder.Options);
    }
}
