using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;
using ZIONShop.Orders.Application.Features.GetReturnRequest;
using ZIONShop.Orders.Application.Features.MarkShippedBack;
using ZIONShop.Orders.Application.Features.RequestReturn;

namespace ZIONShop.Api.Controllers;

/// <summary>Customer-facing return endpoints. Owner-only; ownership is enforced in the MediatR handlers
/// (mirrors <c>OrdersController.Cancel</c>'s pattern), not here.</summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class ReturnsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ReturnsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpPost("orders/{id:guid}/return")]
    public async Task<IActionResult> RequestReturn(Guid id, [FromBody] RequestReturnBody body, CancellationToken ct)
        => (await _mediator.Send(new RequestReturnCommand(id, _currentUser.UserId!.Value, body.Reason, body.Note), ct))
            .ToActionResult("Return request created");

    /// <summary>Returns the order's latest return request, or a successful response with null <c>data</c>
    /// when the order has no return request yet. Only 404s if the order itself doesn't exist / isn't the
    /// caller's.</summary>
    [HttpGet("orders/{id:guid}/return")]
    public async Task<IActionResult> GetReturn(Guid id, CancellationToken ct)
        => (await _mediator.Send(new GetReturnRequestQuery(id, _currentUser.UserId!.Value, IsStaffOrAdmin: false), ct)).ToActionResult();

    [HttpPost("returns/{id:guid}/shipped-back")]
    public async Task<IActionResult> MarkShippedBack(Guid id, [FromBody] MarkShippedBackBody? body, CancellationToken ct)
        => (await _mediator.Send(new MarkShippedBackCommand(id, _currentUser.UserId!.Value, body?.TrackingCode), ct))
            .ToActionResult("Marked as shipped back");
}

public record RequestReturnBody(string Reason, string? Note);
public record MarkShippedBackBody(string? TrackingCode);
