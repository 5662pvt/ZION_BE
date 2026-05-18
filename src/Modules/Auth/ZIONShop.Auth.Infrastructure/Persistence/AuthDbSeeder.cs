using Microsoft.EntityFrameworkCore;
using ZIONShop.Auth.Domain.Entities;

namespace ZIONShop.Auth.Infrastructure.Persistence;

public static class AuthDbSeeder
{
    public static async Task SeedAsync(AuthDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        var adminEmail = "admin@zionshop.local";
        if (!await db.Users.AnyAsync(u => u.Email == adminEmail, cancellationToken))
        {
            var hash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
            var admin = User.Register(adminEmail, hash, "ZIONShop Admin", new[] { "Admin", "Customer" });
            await db.Users.AddAsync(admin, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
