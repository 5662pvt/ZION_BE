using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;
using ZIONShop.Promotions.Application.Features.GetGiftPreview;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/promotions/gifts")]
[Authorize]
public class GiftsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public GiftsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(CancellationToken ct)
        => (await _mediator.Send(new GetGiftPreviewQuery(_currentUser.UserId!.Value), ct)).ToActionResult();
}
