using ZIONShop.Users.Application.Interfaces;
using ZIONShop.Users.Domain.Entities;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Services;

public class UserProfileProvisioner : IUserProfileProvisioner
{
    private readonly IUserProfileRepository _profiles;
    private readonly IUsersUnitOfWork _uow;

    public UserProfileProvisioner(IUserProfileRepository profiles, IUsersUnitOfWork uow)
    {
        _profiles = profiles;
        _uow = uow;
    }

    public async Task<UserProfile> EnsureAsync(Guid authUserId, string email, string? displayName = null, CancellationToken cancellationToken = default)
    {
        var profile = await _profiles.GetByAuthUserIdAsync(authUserId, cancellationToken);
        if (profile is not null) return profile;

        profile = UserProfile.Create(authUserId, email, displayName);
        await _profiles.AddAsync(profile, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return profile;
    }
}
