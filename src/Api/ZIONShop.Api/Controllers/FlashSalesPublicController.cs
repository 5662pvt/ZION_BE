using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Common.Api;
using ZIONShop.Promotions.Application.Features.GetActiveFlashSales;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/flash-sales")]
public class FlashSalesPublicController : ControllerBase
{
    private readonly IMediator _mediator;

    public FlashSalesPublicController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActive(CancellationToken ct)
        => (await _mediator.Send(new GetActiveFlashSalesQuery(), ct)).ToActionResult();
}
