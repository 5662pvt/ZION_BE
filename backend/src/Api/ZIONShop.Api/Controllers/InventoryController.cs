using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.Authorization;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;
using ZIONShop.Inventory.Application.Features.AdjustStock;
using ZIONShop.Inventory.Application.Features.GetAvailability;
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
        => (await _mediator.Send(new GetAvailabilityQuery(productId), ct)).ToActionResult();

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
        => (await _mediator.Send(new GetStockMovementsQuery(productId, page, pageSize), ct)).ToPagedActionResult();

    [HttpPost("products/{productId:guid}/adjust")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> AdjustStock(Guid productId, [FromBody] AdjustStockBody body, CancellationToken ct)
    {
        var createdBy = _currentUser.Email ?? _currentUser.UserId?.ToString();
        var cmd = new AdjustStockCommand(productId, body.Delta, body.Reason, createdBy);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Stock adjusted");
    }
}

public record SearchStockRequest(int Page = 1, int PageSize = 20, string? Keyword = null, bool LowStockOnly = false);
public record AdjustStockBody(int Delta, string Reason);
