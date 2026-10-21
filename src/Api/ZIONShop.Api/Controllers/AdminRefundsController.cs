using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.Authorization;
using ZIONShop.Common.Api;
using ZIONShop.Payments.Application.Features.ListRefunds;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/admin/refunds")]
[Authorize(Policy = Policies.AdminOnly)]
public class AdminRefundsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminRefundsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminListRefundsRequest req, CancellationToken ct)
    {
        var query = new ListRefundsQuery(req.Page <= 0 ? 1 : req.Page, req.PageSize <= 0 ? 20 : req.PageSize, req.Status);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }
}

public record AdminListRefundsRequest(int Page = 1, int PageSize = 20, string? Status = null);
