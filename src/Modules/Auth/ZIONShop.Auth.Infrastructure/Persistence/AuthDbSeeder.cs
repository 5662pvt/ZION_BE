using Microsoft.EntityFrameworkCore;
using ZIONShop.Auth.Domain.Entities;

namespace ZIONShop.Auth.Infrastructure.Persistence;

public static class AuthDbSeeder
{
    private const int MaxLoadTestCustomers = 500;

    /// <summary><paramref name="loadTestCustomers"/> is clamped to [0, <see cref="MaxLoadTestCustomers"/>]
    /// so a misconfigured env var can't seed an unbounded number of rows.</summary>
    public static async Task SeedAsync(AuthDbContext db, int loadTestCustomers = 0, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        await SeedFixedUserAsync(db, "admin@zionshop.local", "Admin@123", "ZIONShop Admin", new[] { "Admin", "Customer" }, cancellationToken);
        await SeedFixedUserAsync(db, "staff@zionshop.local", "Staff@123", "ZIONShop Staff", new[] { "Staff" }, cancellationToken);
        await SeedFixedUserAsync(db, "customer@zionshop.local", "Customer@123", "ZIONShop Customer", new[] { "Customer" }, cancellationToken);

        await SeedLoadTestCustomersAsync(db, Math.Clamp(loadTestCustomers, 0, MaxLoadTestCustomers), cancellationToken);
    }

    private static async Task SeedFixedUserAsync(
        AuthDbContext db, string email, string password, string displayName, string[] roles, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            user = User.Register(email, hash, displayName, roles);
            user.ConfirmEmail();
            await db.Users.AddAsync(user, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (!user.EmailConfirmed)
        {
            user.ConfirmEmail();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Seeds <c>load1@zionshop.local</c>..<c>load{count}@zionshop.local</c>, all sharing one
    /// pre-hashed password (BCrypt hashing is deliberately slow -- hashing it once instead of per-user
    /// keeps this from stalling startup when count is large). Looks up existing emails in a single
    /// query and only inserts what's missing, in one batched SaveChanges, so re-running is a no-op.</summary>
    private static async Task SeedLoadTestCustomersAsync(AuthDbContext db, int count, CancellationToken cancellationToken)
    {
        if (count == 0) return;

        // StartsWith/EndsWith on a literal translates to a plain LIKE -- unlike a closure array's
        // Contains(), which EF's expression evaluator cannot reliably compile/execute client-side here.
        var existing = (await db.Users
            .Where(u => u.Email.StartsWith("load") && u.Email.EndsWith("@zionshop.local"))
            .Select(u => u.Email)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var missingEmails = Enumerable.Range(1, count)
            .Select(i => (Index: i, Email: $"load{i}@zionshop.local"))
            .Where(x => !existing.Contains(x.Email))
            .ToList();

        if (missingEmails.Count == 0) return;

        var hash = BCrypt.Net.BCrypt.HashPassword("Load@123");
        var newUsers = missingEmails.Select(x =>
        {
            var user = User.Register(x.Email, hash, $"Load Test {x.Index}", new[] { "Customer" });
            user.ConfirmEmail();
            return user;
        });

        await db.Users.AddRangeAsync(newUsers, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
