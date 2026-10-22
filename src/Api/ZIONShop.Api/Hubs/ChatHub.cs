using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ZIONShop.Auth.Authorization;
using ZIONShop.Chat.Application.Features.GetConversationById;
using ZIONShop.Chat.Application.Features.GetConversationIdForCustomer;
using ZIONShop.Realtime;

namespace ZIONShop.Api.Hubs;

/// <summary>
/// Resolves conversations exclusively through MediatR (<see cref="IMediator"/>) -- must not reference
/// Chat.Infrastructure directly. Staff/Admin connections join the shared "staff" group on connect
/// (mirrors the shared, unassigned inbox); customers stay on their own per-user SignalR identity
/// (<c>ClaimsUserIdProvider</c>), targeted via <see cref="IRealtimeEventPublisher.PublishToUserAsync"/>.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private const string StaffGroup = "staff";

    private readonly IMediator _mediator;
    private readonly IRealtimeEventPublisher _realtime;

    public ChatHub(IMediator mediator, IRealtimeEventPublisher realtime)
    {
        _mediator = mediator;
        _realtime = realtime;
    }

    public override async Task OnConnectedAsync()
    {
        if (IsStaffOrAdmin(Context.User))
            await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup);

        await base.OnConnectedAsync();
    }

    /// <summary>A customer calls <c>Typing(null)</c> (server resolves their own conversation id); staff
    /// calls <c>Typing(conversationId)</c>. Silently does nothing if there is nothing to relay (no
    /// conversation yet, or an unknown conversation id).</summary>
    public async Task Typing(Guid? conversationId)
    {
        var principal = Context.User;
        if (principal is null) return;

        if (IsStaffOrAdmin(principal))
        {
            if (conversationId is not { } id) return;

            var conversation = await _mediator.Send(new GetConversationByIdQuery(id));
            if (conversation.IsFailure) return;

            await _realtime.PublishToUserAsync(conversation.Value.CustomerId, "chat:typing", new { conversationId = id, from = "Staff" });
        }
        else
        {
            var userId = GetUserId(principal);
            if (userId is null) return;

            var resolved = await _mediator.Send(new GetConversationIdForCustomerQuery(userId.Value));
            if (resolved.IsFailure || resolved.Value is not { } id) return;

            await _realtime.PublishToStaffAsync("chat:typing", new { conversationId = id, from = "Customer" });
        }
    }

    private static bool IsStaffOrAdmin(ClaimsPrincipal? principal) =>
        principal is not null && (principal.IsInRole(Roles.Staff) || principal.IsInRole(Roles.Admin));

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
