using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ZIONShop.Reviews.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef migrations add` — avoids building the full app host (and its module DI graph) just to generate a migration.</summary>
public class ReviewsDbContextFactory : IDesignTimeDbContextFactory<ReviewsDbContext>
{
    public ReviewsDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ReviewsDbContext>();
        builder.UseNpgsql("Host=localhost;Database=ZionShop_designtime",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ReviewsDbContext.Schema));
        return new ReviewsDbContext(builder.Options);
    }
}
