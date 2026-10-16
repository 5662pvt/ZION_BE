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

        var staffEmail = "staff@zionshop.local";
        var staff = await db.Users.FirstOrDefaultAsync(u => u.Email == staffEmail, cancellationToken);
        if (staff is null)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword("Staff@123");
            staff = User.Register(staffEmail, hash, "ZIONShop Staff", new[] { "Staff" });
            staff.ConfirmEmail();
            await db.Users.AddAsync(staff, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (!staff.EmailConfirmed)
        {
            staff.ConfirmEmail();
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
