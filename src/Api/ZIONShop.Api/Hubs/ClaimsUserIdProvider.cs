using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace ZIONShop.Api.Hubs;

/// <summary>
/// Must resolve the exact same claim as <see cref="ZIONShop.Auth.CurrentUser.CurrentUserService.UserId"/>
/// (Sub, falling back to NameIdentifier) so that <c>Clients.User(userId)</c> in
/// <c>NotificationRelayHostedService</c> matches the connection registered for that user.
/// </summary>
public class ClaimsUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var principal = connection.User;
        return principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
               ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
