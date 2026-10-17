using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ZIONShop.Api.Hubs;

/// <summary>Server -> client push only (via <see cref="IHubContext{NotificationsHub}"/> from <c>NotificationRelayHostedService</c>); no client-invokable methods needed yet.</summary>
[Authorize]
public class NotificationsHub : Hub
{
}
