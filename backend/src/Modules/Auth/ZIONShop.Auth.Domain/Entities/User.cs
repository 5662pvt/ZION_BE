using ZIONShop.Auth.Domain.Events;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Auth.Domain.Entities;

public class User : AggregateRoot
{
    private readonly List<RefreshToken> _refreshTokens = new();
    private string _rolesRaw = "Customer";

    private User() { }

    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAt { get; private set; }

    public string RolesRaw
    {
        get => _rolesRaw;
        private set => _rolesRaw = value ?? string.Empty;
    }

    public IReadOnlyCollection<string> Roles =>
        string.IsNullOrEmpty(_rolesRaw)
            ? Array.Empty<string>()
            : _rolesRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public static User Register(string email, string passwordHash, string? displayName, IEnumerable<string>? roles = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            DisplayName = displayName?.Trim()
        };
        var roleList = (roles ?? new[] { "Customer" }).ToList();
        user._rolesRaw = string.Join(';', roleList);
        user.AddDomainEvent(new UserRegisteredDomainEvent(user.Id, user.Email, user.DisplayName));
        return user;
    }

    public void RecordLogin() => LastLoginAt = DateTime.UtcNow;

    public void AddRefreshToken(RefreshToken token) => _refreshTokens.Add(token);

    public void RevokeRefreshToken(string tokenHash)
    {
        var existing = _refreshTokens.FirstOrDefault(rt => rt.TokenHash == tokenHash);
        existing?.Revoke();
    }

    public void Deactivate() => IsActive = false;
}
