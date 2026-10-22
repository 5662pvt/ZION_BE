using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ZIONShop.Chat.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef migrations add` — avoids building the full app host (and its module DI graph) just to generate a migration.</summary>
public class ChatDbContextFactory : IDesignTimeDbContextFactory<ChatDbContext>
{
    public ChatDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ChatDbContext>();
        builder.UseNpgsql("Host=localhost;Database=ZionShop_designtime",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ChatDbContext.Schema));
        return new ChatDbContext(builder.Options);
    }
}
