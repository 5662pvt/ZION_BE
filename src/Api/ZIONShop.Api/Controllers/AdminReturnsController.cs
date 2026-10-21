using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Api.Filters;
using ZIONShop.Auth.Authorization;
using ZIONShop.Common.Api;
using ZIONShop.Orders.Application.Features.ApproveReturn;
using ZIONShop.Orders.Application.Features.GetReturnById;
using ZIONShop.Orders.Application.Features.MarkReceived;
using ZIONShop.Orders.Application.Features.RejectReturn;
using ZIONShop.Orders.Application.Features.SearchReturns;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/admin/returns")]
[Authorize(Policy = Policies.StaffOrAdmin)]
public class AdminReturnsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminReturnsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] AdminSearchReturnsRequest req, CancellationToken ct)
    {
        var query = new SearchReturnsQuery(req.Page <= 0 ? 1 : req.Page, req.PageSize <= 0 ? 20 : req.PageSize, req.Status);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await _mediator.Send(new GetReturnByIdQuery(id), ct)).ToActionResult();

    [HttpPost("{id:guid}/approve")]
    [AdminAudit("Return.Approve", "ReturnRequest")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
        => (await _mediator.Send(new ApproveReturnCommand(id), ct)).ToActionResult("Return request approved");

    [HttpPost("{id:guid}/reject")]
    [AdminAudit("Return.Reject", "ReturnRequest")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectReturnBody body, CancellationToken ct)
        => (await _mediator.Send(new RejectReturnCommand(id, body.Reason), ct)).ToActionResult("Return request rejected");

    [HttpPost("{id:guid}/received")]
    [AdminAudit("Return.Received", "ReturnRequest")]
    public async Task<IActionResult> MarkReceived(Guid id, CancellationToken ct)
        => (await _mediator.Send(new MarkReceivedCommand(id), ct)).ToActionResult("Return marked as received");
}

public record RejectReturnBody(string Reason);
public record AdminSearchReturnsRequest(int Page = 1, int PageSize = 20, string? Status = null);
