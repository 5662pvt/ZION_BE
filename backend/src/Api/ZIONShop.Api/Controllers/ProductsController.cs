using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.Authorization;
using ZIONShop.Common.Api;
using ZIONShop.Products.Application.Features.CreateProduct;
using ZIONShop.Products.Application.Features.GetProduct;
using ZIONShop.Products.Application.Features.SearchProducts;
using ZIONShop.Products.Application.Features.UpdateProduct;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] SearchProductsRequest req, CancellationToken ct)
    {
        var query = new SearchProductsQuery(
            req.Page <= 0 ? 1 : req.Page,
            req.PageSize <= 0 ? 20 : req.PageSize,
            req.Keyword,
            req.CategoryId,
            req.MinPrice,
            req.MaxPrice,
            req.Sort);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await _mediator.Send(new GetProductByIdQuery(id), ct)).ToActionResult();

    [HttpGet("by-slug/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
        => (await _mediator.Send(new GetProductBySlugQuery(slug), ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command, CancellationToken ct)
        => (await _mediator.Send(command, ct)).ToActionResult("Product created");

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductBody body, CancellationToken ct)
    {
        var cmd = new UpdateProductCommand(id, body.Name, body.Description, body.Price, body.Currency, body.CategoryId, body.Brand);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Product updated");
    }
}

public record SearchProductsRequest(int Page = 1, int PageSize = 20, string? Keyword = null, Guid? CategoryId = null, decimal? MinPrice = null, decimal? MaxPrice = null, string? Sort = null);
public record UpdateProductBody(string Name, string? Description, decimal Price, string Currency, Guid? CategoryId, string? Brand);
