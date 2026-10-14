using Microsoft.EntityFrameworkCore;
using ZIONShop.Auth.Domain.Entities;

namespace ZIONShop.Auth.Infrastructure.Persistence;

public static class AuthDbSeeder
{
    public static async Task SeedAsync(AuthDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        var adminEmail = "admin@zionshop.local";
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == adminEmail, cancellationToken);
        if (admin is null)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
            admin = User.Register(adminEmail, hash, "ZIONShop Admin", new[] { "Admin", "Customer" });
            admin.ConfirmEmail();
            await db.Users.AddAsync(admin, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (!admin.EmailConfirmed)
        {
            admin.ConfirmEmail();
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
