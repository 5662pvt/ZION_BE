using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;
using ZIONShop.Promotions.Application.Features.ValidateCoupon;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/promotions/coupons")]
[Authorize]
public class CouponsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public CouponsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateCoupon([FromBody] ValidateCouponBody body, CancellationToken ct)
        => (await _mediator.Send(new ValidateCouponCommand(_currentUser.UserId!.Value, body.Code), ct)).ToActionResult();
}

/// <summary>Subtotal is accepted for backward compatibility but ignored; BE computes it from the caller's cart.</summary>
public record ValidateCouponBody(string Code, decimal? Subtotal = null);
