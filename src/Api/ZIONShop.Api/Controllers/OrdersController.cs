using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.Authorization;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;
using ZIONShop.Orders.Application.Features.CancelOrder;
using ZIONShop.Orders.Application.Features.CompleteOrder;
using ZIONShop.Orders.Application.Features.GetMyOrders;
using ZIONShop.Orders.Application.Features.GetOrder;
using ZIONShop.Orders.Application.Features.PlaceOrder;
using ZIONShop.Orders.Application.Features.SearchOrders;
using ZIONShop.Orders.Application.Features.ShipOrder;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public OrdersController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    private bool IsStaffOrAdmin => _currentUser.Roles.Contains(Roles.Staff) || _currentUser.Roles.Contains(Roles.Admin);

    [HttpPost]
    public async Task<IActionResult> Place([FromBody] PlaceOrderBody body, CancellationToken ct)
    {
        var cmd = new PlaceOrderCommand(_currentUser.UserId!.Value, body.AddressId, body.PaymentMethod, body.Note, body.CouponCode);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Order placed");
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] GetMyOrdersRequest req, CancellationToken ct)
    {
        var query = new GetMyOrdersQuery(_currentUser.UserId!.Value, req.Page <= 0 ? 1 : req.Page, req.PageSize <= 0 ? 20 : req.PageSize, req.Status);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await _mediator.Send(new GetOrderQuery(id, _currentUser.UserId!.Value, IsStaffOrAdmin), ct)).ToActionResult();

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelOrderBody? body, CancellationToken ct)
        => (await _mediator.Send(new CancelOrderCommand(id, _currentUser.UserId!.Value, IsStaffOrAdmin, body?.Reason), ct)).ToActionResult("Order cancelled");

    [HttpGet]
    [Authorize(Policy = Policies.StaffOrAdmin)]
    public async Task<IActionResult> AdminSearch([FromQuery] AdminSearchOrdersRequest req, CancellationToken ct)
    {
        var query = new SearchOrdersQuery(req.Page <= 0 ? 1 : req.Page, req.PageSize <= 0 ? 20 : req.PageSize, req.Status, req.From, req.To, req.Keyword);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }

    [HttpPost("{id:guid}/ship")]
    [Authorize(Policy = Policies.StaffOrAdmin)]
    public async Task<IActionResult> Ship(Guid id, CancellationToken ct)
        => (await _mediator.Send(new ShipOrderCommand(id), ct)).ToActionResult("Order shipped");

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = Policies.StaffOrAdmin)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
        => (await _mediator.Send(new CompleteOrderCommand(id), ct)).ToActionResult("Order completed");
}

public record PlaceOrderBody(Guid AddressId, string PaymentMethod, string? Note, string? CouponCode = null);
public record CancelOrderBody(string? Reason);
public record GetMyOrdersRequest(int Page = 1, int PageSize = 20, string? Status = null);
public record AdminSearchOrdersRequest(int Page = 1, int PageSize = 20, string? Status = null, DateTime? From = null, DateTime? To = null, string? Keyword = null);
