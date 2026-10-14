using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.Authorization;
using ZIONShop.Common.Api;
using ZIONShop.Notifications.Application.Features.GetNotificationLogs;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/admin/notifications")]
[Authorize(Policy = Policies.AdminOnly)]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetLogs([FromQuery] GetNotificationLogsRequest req, CancellationToken ct)
    {
        var query = new GetNotificationLogsQuery(req.Page <= 0 ? 1 : req.Page, req.PageSize <= 0 ? 20 : req.PageSize, req.OrderId);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }
}

public record GetNotificationLogsRequest(int Page = 1, int PageSize = 20, Guid? OrderId = null);
