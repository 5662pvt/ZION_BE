using ZIONShop.Auth.Domain.Enums;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Auth.Domain.Entities;

public class AuthOtp : BaseEntity
{
    private AuthOtp() { }

    public Guid UserId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public AuthOtpPurpose Purpose { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public DateTime CreatedDate { get; private set; }

    public bool IsActive => UsedAt is null && ExpiresAt > DateTime.UtcNow;

    public static AuthOtp Create(Guid userId, string email, AuthOtpPurpose purpose, string codeHash, DateTime expiresAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = email.Trim().ToLowerInvariant(),
            Purpose = purpose,
            CodeHash = codeHash,
            ExpiresAt = expiresAt,
            CreatedDate = DateTime.UtcNow
        };

    public bool Verify(string codeHash) => IsActive && CodeHash == codeHash;

    public void MarkUsed() => UsedAt = DateTime.UtcNow;
}
