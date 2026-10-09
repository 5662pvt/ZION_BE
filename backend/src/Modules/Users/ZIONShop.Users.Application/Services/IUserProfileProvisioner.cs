using ZIONShop.Users.Domain.Entities;

namespace ZIONShop.Users.Application.Services;

public interface IUserProfileProvisioner
{
    Task<UserProfile> EnsureAsync(Guid authUserId, string email, string? displayName = null, CancellationToken cancellationToken = default);
}
