using Microsoft.EntityFrameworkCore;
using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Domain.Enums;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.Auth.Infrastructure.Persistence;

namespace ZIONShop.Auth.Infrastructure.Repositories;

public class AuthOtpRepository : IAuthOtpRepository
{
    private readonly AuthDbContext _db;

    public AuthOtpRepository(AuthDbContext db) => _db = db;

    public Task<AuthOtp?> GetActiveByEmailAndPurposeAsync(string email, AuthOtpPurpose purpose, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        return _db.AuthOtps
            .Where(o => o.Email == normalized && o.Purpose == purpose && o.UsedAt == null && o.ExpiresAt > now)
            .OrderByDescending(o => o.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task InvalidateActiveByUserAndPurposeAsync(Guid userId, AuthOtpPurpose purpose, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var active = await _db.AuthOtps
            .Where(o => o.UserId == userId && o.Purpose == purpose && o.UsedAt == null && o.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var otp in active)
            otp.MarkUsed();
    }

    public async Task AddAsync(AuthOtp otp, CancellationToken cancellationToken = default) =>
        await _db.AuthOtps.AddAsync(otp, cancellationToken);
}
