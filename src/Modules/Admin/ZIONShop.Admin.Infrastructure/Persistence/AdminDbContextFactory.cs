using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ZIONShop.Admin.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef migrations add` — avoids building the full app host (and its module DI graph) just to generate a migration.</summary>
public class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<AdminDbContext>();
        builder.UseNpgsql("Host=localhost;Database=ZionShop_designtime",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AdminDbContext.Schema));
        return new AdminDbContext(builder.Options);
    }
}
