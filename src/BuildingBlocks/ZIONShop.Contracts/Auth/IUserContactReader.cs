namespace ZIONShop.Contracts.Auth;

public sealed record UserContact(Guid UserId, string Email, string? DisplayName);

public interface IUserContactReader
{
    Task<UserContact?> GetAsync(Guid userId, CancellationToken ct = default);
}
