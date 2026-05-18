using MediatR;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Cart.Application.Features.AddItem;
using ZIONShop.Cart.Application.Features.ClearCart;
using ZIONShop.Cart.Application.Features.GetCart;
using ZIONShop.Cart.Application.Features.RemoveItem;
using ZIONShop.Cart.Application.Features.UpdateItem;
using ZIONShop.Common.Api;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/cart")]
public class CartController : ControllerBase
{
    public const string AnonHeader = "X-Cart-Token";

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public CartController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var anonymous = ResolveAnonymous();
        var result = await _mediator.Send(new GetCartQuery(_currentUser.UserId, anonymous), ct);
        return result.ToActionResult();
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddCartItemBody body, CancellationToken ct)
    {
        var anonymous = ResolveAnonymous();
        var result = await _mediator.Send(new AddCartItemCommand(_currentUser.UserId, anonymous, body.ProductId, body.Quantity), ct);
        return result.ToActionResult("Item added");
    }

    [HttpPut("items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid itemId, [FromBody] UpdateCartItemBody body, CancellationToken ct)
    {
        var anonymous = ResolveAnonymous();
        var result = await _mediator.Send(new UpdateCartItemCommand(_currentUser.UserId, anonymous, itemId, body.Quantity), ct);
        return result.ToActionResult("Item updated");
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid itemId, CancellationToken ct)
    {
        var anonymous = ResolveAnonymous();
        var result = await _mediator.Send(new RemoveCartItemCommand(_currentUser.UserId, anonymous, itemId), ct);
        return result.ToActionResult("Item removed");
    }

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        var anonymous = ResolveAnonymous();
        var result = await _mediator.Send(new ClearCartCommand(_currentUser.UserId, anonymous), ct);
        return result.ToActionResult("Cart cleared");
    }

    private string? ResolveAnonymous()
    {
        if (_currentUser.UserId.HasValue) return null;
        if (Request.Headers.TryGetValue(AnonHeader, out var v) && !string.IsNullOrWhiteSpace(v))
            return v.ToString();
        var generated = Guid.NewGuid().ToString("N");
        Response.Headers[AnonHeader] = generated;
        return generated;
    }
}

public record AddCartItemBody(Guid ProductId, int Quantity);
public record UpdateCartItemBody(int Quantity);
