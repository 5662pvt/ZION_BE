using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Api.Filters;
using ZIONShop.Auth.Authorization;
using ZIONShop.Common.Api;
using ZIONShop.Promotions.Application.Features.ActivateFlashSale;
using ZIONShop.Promotions.Application.Features.AddFlashSaleItem;
using ZIONShop.Promotions.Application.Features.CreateFlashSale;
using ZIONShop.Promotions.Application.Features.DeactivateFlashSale;
using ZIONShop.Promotions.Application.Features.GetFlashSaleById;
using ZIONShop.Promotions.Application.Features.SearchFlashSales;
using ZIONShop.Promotions.Application.Features.UpdateFlashSale;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/admin/flash-sales")]
[Authorize(Policy = Policies.AdminOnly)]
public class FlashSalesController : ControllerBase
{
    private readonly IMediator _mediator;

    public FlashSalesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] SearchFlashSalesRequest req, CancellationToken ct)
    {
        var query = new SearchFlashSalesQuery(req.Page <= 0 ? 1 : req.Page, req.PageSize <= 0 ? 20 : req.PageSize, req.Keyword);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await _mediator.Send(new GetFlashSaleByIdQuery(id), ct)).ToActionResult();

    [HttpPost]
    [AdminAudit("FlashSale.Create", "FlashSale", ResourceIdFromResult = true)]
    public async Task<IActionResult> Create([FromBody] FlashSaleUpsertRequest body, CancellationToken ct)
        => (await _mediator.Send(new CreateFlashSaleCommand(body.Name, body.StartsAt, body.EndsAt), ct)).ToActionResult("Flash sale created");

    [HttpPut("{id:guid}")]
    [AdminAudit("FlashSale.Update", "FlashSale")]
    public async Task<IActionResult> Update(Guid id, [FromBody] FlashSaleUpsertRequest body, CancellationToken ct)
        => (await _mediator.Send(new UpdateFlashSaleCommand(id, body.Name, body.StartsAt, body.EndsAt), ct)).ToActionResult("Flash sale updated");

    [HttpPost("{id:guid}/activate")]
    [AdminAudit("FlashSale.Activate", "FlashSale")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
        => (await _mediator.Send(new ActivateFlashSaleCommand(id), ct)).ToActionResult("Flash sale activated");

    [HttpPost("{id:guid}/deactivate")]
    [AdminAudit("FlashSale.Deactivate", "FlashSale")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => (await _mediator.Send(new DeactivateFlashSaleCommand(id), ct)).ToActionResult("Flash sale deactivated");

    [HttpPost("{id:guid}/items")]
    [AdminAudit("FlashSaleItem.Add", "FlashSale")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddFlashSaleItemBody body, CancellationToken ct)
        => (await _mediator.Send(new AddFlashSaleItemCommand(id, body.ProductId, body.VariantId, body.FlashPrice, body.AllocatedQuantity), ct)).ToActionResult("Flash sale item added");
}

public record SearchFlashSalesRequest(int Page = 1, int PageSize = 20, string? Keyword = null);
public record FlashSaleUpsertRequest(string Name, DateTime StartsAt, DateTime EndsAt);
public record AddFlashSaleItemBody(Guid? ProductId, Guid? VariantId, decimal FlashPrice, int AllocatedQuantity);
