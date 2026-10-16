namespace ZIONShop.Contracts.Users;

public sealed record UserProfileSnapshot(Guid AuthUserId, string? FullName, string? PhoneNumber, int AddressesCount);

public interface IUserProfileReader
{
    Task<IReadOnlyList<UserProfileSnapshot>> GetProfilesAsync(IReadOnlyCollection<Guid> authUserIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> FindAuthUserIdsByPhoneAsync(string phone, CancellationToken cancellationToken = default);
}
