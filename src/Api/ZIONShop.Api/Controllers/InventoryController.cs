using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Api.Filters;
using ZIONShop.Auth.Authorization;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;
using ZIONShop.Inventory.Application.Features.AdjustStock;
using ZIONShop.Inventory.Application.Features.GetAvailability;
using ZIONShop.Inventory.Application.Features.GetAvailabilityBatch;
using ZIONShop.Inventory.Application.Features.GetProductStockSummary;
using ZIONShop.Inventory.Application.Features.GetStockMovements;
using ZIONShop.Inventory.Application.Features.SearchStock;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public InventoryController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("products/{productId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailability(Guid productId, CancellationToken ct)
        => (await _mediator.Send(new GetAvailabilityQuery(productId, IsVariantId: false), ct)).ToActionResult();

    [HttpGet("variants/{variantId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVariantAvailability(Guid variantId, CancellationToken ct)
        => (await _mediator.Send(new GetAvailabilityQuery(variantId, IsVariantId: true), ct)).ToActionResult();

    [HttpGet("availability")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailabilityBatch([FromQuery] string? productIds, CancellationToken ct)
    {
        var (ids, error) = ParseGuidList(productIds, "productIds");
        if (error is not null) return error;

        return (await _mediator.Send(new GetAvailabilityBatchQuery(ids), ct)).ToActionResult();
    }

    [HttpGet("availability/variants")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailabilityByVariants([FromQuery] string? variantIds, CancellationToken ct)
    {
        var (ids, error) = ParseGuidList(variantIds, "variantIds");
        if (error is not null) return error;

        return (await _mediator.Send(new GetAvailabilityBatchQuery(ids), ct)).ToActionResult();
    }

    [HttpGet("availability/products")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailabilityByProducts([FromQuery] string? productIds, CancellationToken ct)
    {
        var (ids, error) = ParseGuidList(productIds, "productIds");
        if (error is not null) return error;

        return (await _mediator.Send(new GetProductStockSummaryQuery(ids), ct)).ToActionResult();
    }

    [HttpGet]
    [Authorize(Policy = Policies.StaffOrAdmin)]
    public async Task<IActionResult> Search([FromQuery] SearchStockRequest req, CancellationToken ct)
    {
        var query = new SearchStockQuery(
            req.Page <= 0 ? 1 : req.Page,
            req.PageSize <= 0 ? 20 : req.PageSize,
            req.Keyword,
            req.LowStockOnly);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }

    [HttpGet("products/{productId:guid}/movements")]
    [Authorize(Policy = Policies.StaffOrAdmin)]
    public async Task<IActionResult> GetMovements(Guid productId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await _mediator.Send(new GetStockMovementsQuery(productId, false, page, pageSize), ct)).ToPagedActionResult();

    [HttpGet("variants/{variantId:guid}/movements")]
    [Authorize(Policy = Policies.StaffOrAdmin)]
    public async Task<IActionResult> GetVariantMovements(Guid variantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await _mediator.Send(new GetStockMovementsQuery(variantId, true, page, pageSize), ct)).ToPagedActionResult();

    [HttpPost("products/{productId:guid}/adjust")]
    [Authorize(Policy = Policies.AdminOnly)]
    [AdminAudit("Inventory.Adjust", "Inventory", RouteKey = "productId")]
    public async Task<IActionResult> AdjustStock(Guid productId, [FromBody] AdjustStockBody body, CancellationToken ct)
    {
        var createdBy = _currentUser.Email ?? _currentUser.UserId?.ToString();
        var cmd = new AdjustStockCommand(productId, false, body.Delta, body.Reason, createdBy);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Stock adjusted");
    }

    [HttpPost("variants/{variantId:guid}/adjust")]
    [Authorize(Policy = Policies.AdminOnly)]
    [AdminAudit("Inventory.Adjust", "Inventory", RouteKey = "variantId")]
    public async Task<IActionResult> AdjustVariantStock(Guid variantId, [FromBody] AdjustStockBody body, CancellationToken ct)
    {
        var createdBy = _currentUser.Email ?? _currentUser.UserId?.ToString();
        var cmd = new AdjustStockCommand(variantId, true, body.Delta, body.Reason, createdBy);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Stock adjusted");
    }

    private (List<Guid> Ids, IActionResult? Error) ParseGuidList(string? raw, string field)
    {
        var ids = new List<Guid>();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(part, out var id))
                {
                    var errors = new List<ErrorDetail> { new() { Field = field, Message = $"'{part}' không phải GUID hợp lệ" } };
                    return (ids, BadRequest(ApiResponse.Fail($"{field} không hợp lệ", errors)));
                }
                ids.Add(id);
            }
        }

        if (ids.Count > 100)
        {
            var errors = new List<ErrorDetail> { new() { Field = field, Message = $"Tối đa 100 {field} mỗi lần truy vấn" } };
            return (ids, BadRequest(ApiResponse.Fail($"{field} không hợp lệ", errors)));
        }

        return (ids.Distinct().ToList(), null);
    }
}

public record SearchStockRequest(int Page = 1, int PageSize = 20, string? Keyword = null, bool LowStockOnly = false);
public record AdjustStockBody(int Delta, string Reason);
