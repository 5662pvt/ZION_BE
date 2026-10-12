using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.Authorization;
using ZIONShop.Common.Api;
using ZIONShop.Promotions.Application.Features.ActivatePromotion;
using ZIONShop.Promotions.Application.Features.CreateCoupon;
using ZIONShop.Promotions.Application.Features.CreatePromotion;
using ZIONShop.Promotions.Application.Features.DeactivateCoupon;
using ZIONShop.Promotions.Application.Features.DeactivatePromotion;
using ZIONShop.Promotions.Application.Features.GenerateCoupons;
using ZIONShop.Promotions.Application.Features.GetPromotionById;
using ZIONShop.Promotions.Application.Features.SearchPromotions;
using ZIONShop.Promotions.Application.Features.UpdatePromotion;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/promotions")]
[Authorize(Policy = Policies.AdminOnly)]
public class PromotionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PromotionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] SearchPromotionsRequest req, CancellationToken ct)
    {
        var query = new SearchPromotionsQuery(req.Page <= 0 ? 1 : req.Page, req.PageSize <= 0 ? 20 : req.PageSize, req.Keyword, req.Status);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await _mediator.Send(new GetPromotionByIdQuery(id), ct)).ToActionResult();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PromotionUpsertRequest body, CancellationToken ct)
    {
        var cmd = new CreatePromotionCommand(
            body.Name, body.Description, body.Type, body.Value, body.MaxDiscountAmount, body.MinOrderSubtotal,
            body.StartsAt, body.EndsAt, body.UsageLimit, body.PerUserLimit, body.Scope, body.TargetProductIds, body.TargetCategoryIds);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Promotion created");
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PromotionUpsertRequest body, CancellationToken ct)
    {
        var cmd = new UpdatePromotionCommand(
            id, body.Name, body.Description, body.Type, body.Value, body.MaxDiscountAmount, body.MinOrderSubtotal,
            body.StartsAt, body.EndsAt, body.UsageLimit, body.PerUserLimit, body.Scope, body.TargetProductIds, body.TargetCategoryIds);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Promotion updated");
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
        => (await _mediator.Send(new ActivatePromotionCommand(id), ct)).ToActionResult("Promotion activated");

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => (await _mediator.Send(new DeactivatePromotionCommand(id), ct)).ToActionResult("Promotion deactivated");

    [HttpPost("{id:guid}/coupons")]
    public async Task<IActionResult> CreateCoupon(Guid id, [FromBody] CreateCouponBody body, CancellationToken ct)
        => (await _mediator.Send(new CreateCouponCommand(id, body.Code), ct)).ToActionResult("Coupon created");

    [HttpPost("{id:guid}/coupons/generate")]
    public async Task<IActionResult> GenerateCoupons(Guid id, [FromBody] GenerateCouponsBody body, CancellationToken ct)
        => (await _mediator.Send(new GenerateCouponsCommand(id, body.Prefix, body.Count), ct)).ToActionResult("Coupons generated");

    [HttpPost("{id:guid}/coupons/{couponId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateCoupon(Guid id, Guid couponId, CancellationToken ct)
        => (await _mediator.Send(new DeactivateCouponCommand(id, couponId), ct)).ToActionResult("Coupon deactivated");
}

public record SearchPromotionsRequest(int Page = 1, int PageSize = 20, string? Keyword = null, string? Status = null);

public record PromotionUpsertRequest(
    string Name,
    string? Description,
    string Type,
    decimal Value,
    decimal? MaxDiscountAmount,
    decimal MinOrderSubtotal,
    DateTime StartsAt,
    DateTime EndsAt,
    int? UsageLimit,
    int? PerUserLimit,
    string Scope = "Order",
    Guid[]? TargetProductIds = null,
    Guid[]? TargetCategoryIds = null);

public record CreateCouponBody(string Code);
public record GenerateCouponsBody(string Prefix, int Count);
