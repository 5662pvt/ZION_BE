using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZIONShop.Api.Filters;
using ZIONShop.Auth.Authorization;
using ZIONShop.Common.Api;
using ZIONShop.Products.Application.Features.AddProductImage;
using ZIONShop.Products.Application.Features.AdminGetProduct;
using ZIONShop.Products.Application.Features.AdminSearchProducts;
using ZIONShop.Products.Application.Features.ArchiveProduct;
using ZIONShop.Products.Application.Features.CreateProduct;
using ZIONShop.Products.Application.Features.GetProduct;
using ZIONShop.Products.Application.Features.PublishProduct;
using ZIONShop.Products.Application.Features.RemoveProductImage;
using ZIONShop.Products.Application.Features.ReorderProductImages;
using ZIONShop.Products.Application.Features.SearchProducts;
using ZIONShop.Products.Application.Features.SuggestProducts;
using ZIONShop.Products.Application.Features.UpdateProduct;
using ZIONShop.Products.Domain.Enums;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("search")]
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

    [HttpGet("suggest")]
    [AllowAnonymous]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> Suggest([FromQuery] string? q, [FromQuery] int? limit, CancellationToken ct)
        => (await _mediator.Send(new SuggestProductsQuery(q, limit), ct)).ToActionResult();

    [HttpGet("admin")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> AdminSearch([FromQuery] AdminSearchProductsRequest req, CancellationToken ct)
    {
        ProductStatus? status = null;
        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            if (!Enum.TryParse<ProductStatus>(req.Status, ignoreCase: true, out var parsed))
            {
                var error = Error.Validation("Products.InvalidStatus", $"Invalid status '{req.Status}'");
                return Result.Failure<object>(error).ToActionResult();
            }
            status = parsed;
        }

        var query = new AdminSearchProductsQuery(
            req.Page <= 0 ? 1 : req.Page,
            req.PageSize <= 0 ? 20 : req.PageSize,
            req.Keyword,
            req.CategoryId,
            status);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await _mediator.Send(new GetProductByIdQuery(id), ct)).ToActionResult();

    [HttpGet("admin/{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> AdminGetById(Guid id, CancellationToken ct)
        => (await _mediator.Send(new AdminGetProductByIdQuery(id), ct)).ToActionResult();

    [HttpGet("by-slug/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
        => (await _mediator.Send(new GetProductBySlugQuery(slug), ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    [AdminAudit("Product.Create", "Product", ResourceIdFromResult = true)]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command, CancellationToken ct)
        => (await _mediator.Send(command, ct)).ToActionResult("Product created");

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [AdminAudit("Product.Update", "Product")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductBody body, CancellationToken ct)
    {
        var cmd = new UpdateProductCommand(id, body.Name, body.Description, body.Price, body.Currency, body.CategoryId, body.Brand);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Product updated");
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = Policies.AdminOnly)]
    [AdminAudit("Product.Archive", "Product")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
        => (await _mediator.Send(new ArchiveProductCommand(id), ct)).ToActionResult("Product archived");

    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = Policies.AdminOnly)]
    [AdminAudit("Product.Publish", "Product")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
        => (await _mediator.Send(new PublishProductCommand(id), ct)).ToActionResult("Product published");

    [HttpPost("{id:guid}/images")]
    [Authorize(Policy = Policies.AdminOnly)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5_242_880 + 64_000)]
    [AdminAudit("Product.ImageAdd", "Product")]
    public async Task<IActionResult> AddImage(Guid id, IFormFile file, [FromForm] string? alt, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var cmd = new AddProductImageCommand(id, stream, file.FileName, file.ContentType, file.Length, alt);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Image added");
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [AdminAudit("Product.ImageRemove", "Product")]
    public async Task<IActionResult> RemoveImage(Guid id, Guid imageId, CancellationToken ct)
        => (await _mediator.Send(new RemoveProductImageCommand(id, imageId), ct)).ToActionResult("Image removed");

    [HttpPut("{id:guid}/images/order")]
    [Authorize(Policy = Policies.AdminOnly)]
    [AdminAudit("Product.ImageReorder", "Product")]
    public async Task<IActionResult> ReorderImages(Guid id, [FromBody] ReorderProductImagesBody body, CancellationToken ct)
        => (await _mediator.Send(new ReorderProductImagesCommand(id, body.ImageIds), ct)).ToActionResult("Images reordered");
}

public record SearchProductsRequest(int Page = 1, int PageSize = 20, string? Keyword = null, Guid? CategoryId = null, decimal? MinPrice = null, decimal? MaxPrice = null, string? Sort = null);
public record AdminSearchProductsRequest(int Page = 1, int PageSize = 20, string? Keyword = null, Guid? CategoryId = null, string? Status = null);
public record UpdateProductBody(string Name, string? Description, decimal Price, string Currency, Guid? CategoryId, string? Brand);
public record ReorderProductImagesBody(IReadOnlyList<Guid> ImageIds);
