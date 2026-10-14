using ZIONShop.Contracts.Auth;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Users.Application.Services;

namespace ZIONShop.Users.Application.IntegrationEventHandlers;

public class UserRegisteredIntegrationEventHandler : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly IUserProfileProvisioner _provisioner;

    public UserRegisteredIntegrationEventHandler(IUserProfileProvisioner provisioner)
    {
        _provisioner = provisioner;
    }

    public Task HandleAsync(UserRegisteredIntegrationEvent @event, CancellationToken cancellationToken = default) =>
        _provisioner.EnsureAsync(@event.UserId, @event.Email, @event.DisplayName, cancellationToken);
}
