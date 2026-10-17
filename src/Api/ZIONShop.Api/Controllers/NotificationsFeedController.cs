using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;
using ZIONShop.Notifications.Application.Features.GetNotificationFeed;
using ZIONShop.Notifications.Application.Features.GetUnreadNotificationCount;
using ZIONShop.Notifications.Application.Features.MarkAllNotificationsRead;
using ZIONShop.Notifications.Application.Features.MarkNotificationRead;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications/feed")]
public class NotificationsFeedController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public NotificationsFeedController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetFeed([FromQuery] GetNotificationFeedRequest req, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        var query = new GetNotificationFeedQuery(_currentUser.UserId.Value, req.Page <= 0 ? 1 : req.Page, req.PageSize <= 0 ? 20 : req.PageSize, req.UnreadOnly);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        var result = await _mediator.Send(new GetUnreadNotificationCountQuery(_currentUser.UserId.Value), ct);
        if (result.IsFailure) return result.ToActionResult();
        return Ok(ApiResponse<object>.Ok(new { count = result.Value }, "Success"));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        var result = await _mediator.Send(new MarkNotificationReadCommand(id, _currentUser.UserId.Value), ct);
        return result.ToActionResult("Notification marked as read");
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        var result = await _mediator.Send(new MarkAllNotificationsReadCommand(_currentUser.UserId.Value), ct);
        if (result.IsFailure) return result.ToActionResult();
        return Ok(ApiResponse<object>.Ok(new { count = result.Value }, "Notifications marked as read"));
    }
}

public record GetNotificationFeedRequest(int Page = 1, int PageSize = 20, bool UnreadOnly = false);
