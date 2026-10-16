using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;
using ZIONShop.Notifications.Application.Features.GetMyNotifications;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public class MyNotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public MyNotificationsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] GetMyNotificationsRequest req, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        var query = new GetMyNotificationsQuery(_currentUser.UserId.Value, req.Page <= 0 ? 1 : req.Page, req.PageSize <= 0 ? 20 : req.PageSize);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }
}

public record GetMyNotificationsRequest(int Page = 1, int PageSize = 20);
