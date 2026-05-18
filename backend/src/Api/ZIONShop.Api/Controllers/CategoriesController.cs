using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.Authorization;
using ZIONShop.Common.Api;
using ZIONShop.Products.Application.Features.Categories;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
public class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CategoriesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get([FromQuery] bool tree = true, CancellationToken ct = default)
        => (await _mediator.Send(new GetCategoriesQuery(tree), ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryCommand command, CancellationToken ct)
        => (await _mediator.Send(command, ct)).ToActionResult("Category created");
}
