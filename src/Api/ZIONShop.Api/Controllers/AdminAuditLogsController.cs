using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Admin.Application.Features.ListAuditLogs;
using ZIONShop.Auth.Authorization;
using ZIONShop.Common.Api;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/admin/audit-logs")]
[Authorize(Policy = Policies.AdminOnly)]
public class AdminAuditLogsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminAuditLogsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminListAuditLogsRequest req, CancellationToken ct)
    {
        var query = new ListAuditLogsQuery(
            req.Page <= 0 ? 1 : req.Page,
            req.PageSize <= 0 ? 20 : req.PageSize,
            req.Actor, req.Action, req.ResourceType, req.From, req.To);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }
}

public record AdminListAuditLogsRequest(
    int Page = 1, int PageSize = 20,
    string? Actor = null, string? Action = null, string? ResourceType = null,
    DateTime? From = null, DateTime? To = null);
