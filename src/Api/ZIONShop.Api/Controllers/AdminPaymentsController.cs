using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.Authorization;
using ZIONShop.Common.Api;
using ZIONShop.Payments.Application.Features.ListPayments;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/admin/payments")]
[Authorize(Policy = Policies.AdminOnly)]
public class AdminPaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminPaymentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminListPaymentsRequest req, CancellationToken ct)
    {
        var query = new ListPaymentsQuery(
            req.Page <= 0 ? 1 : req.Page,
            req.PageSize <= 0 ? 20 : req.PageSize,
            req.Status, req.Channel, req.From, req.To, req.OrderId);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }
}

public record AdminListPaymentsRequest(
    int Page = 1, int PageSize = 20,
    string? Status = null, string? Channel = null,
    DateTime? From = null, DateTime? To = null,
    Guid? OrderId = null);
