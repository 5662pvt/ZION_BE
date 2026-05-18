using ZIONShop.Contracts.Auth;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Users.Application.Interfaces;
using ZIONShop.Users.Domain.Entities;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.IntegrationEventHandlers;

public class UserRegisteredIntegrationEventHandler : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly IUserProfileRepository _profiles;
    private readonly IUsersUnitOfWork _uow;

    public UserRegisteredIntegrationEventHandler(IUserProfileRepository profiles, IUsersUnitOfWork uow)
    {
        _profiles = profiles;
        _uow = uow;
    }

    public async Task HandleAsync(UserRegisteredIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        if (await _profiles.ExistsForAuthUserAsync(@event.UserId, cancellationToken)) return;
        var profile = UserProfile.Create(@event.UserId, @event.Email, @event.DisplayName);
        await _profiles.AddAsync(profile, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}
