using Microsoft.EntityFrameworkCore;
using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.Auth.Infrastructure.Persistence;

namespace ZIONShop.Auth.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AuthDbContext _db;

    public UserRepository(AuthDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Users.Include(u => u.RefreshTokens).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        _db.RefreshTokens.AddAsync(token, cancellationToken).AsTask();

    public Task SetLastLoginAsync(Guid userId, DateTime at, CancellationToken cancellationToken = default) =>
        _db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, at), cancellationToken);

    public async Task<User?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var userId = await _db.RefreshTokens
            .Where(rt => rt.TokenHash == tokenHash)
            .Select(rt => (Guid?)rt.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (userId is null) return null;
        return await GetByIdAsync(userId.Value, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        _db.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await _db.Users.AddAsync(user, cancellationToken);

    public void Update(User user)
    {
        // Entity from GetByEmail/GetById is already tracked; Update() can break RowVersion concurrency.
        if (_db.Entry(user).State == EntityState.Detached)
            _db.Users.Update(user);
    }
}
