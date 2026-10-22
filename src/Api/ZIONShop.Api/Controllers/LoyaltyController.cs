using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;
using ZIONShop.Loyalty.Application.Features.GetMyLoyaltySummary;
using ZIONShop.Loyalty.Application.Features.GetMyLoyaltyTransactions;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/loyalty")]
[Authorize]
public class LoyaltyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public LoyaltyController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
        => (await _mediator.Send(new GetMyLoyaltySummaryQuery(_currentUser.UserId!.Value), ct)).ToActionResult();

    [HttpGet("me/transactions")]
    public async Task<IActionResult> GetMyTransactions([FromQuery] LoyaltyTransactionsRequest req, CancellationToken ct)
        => (await _mediator.Send(new GetMyLoyaltyTransactionsQuery(_currentUser.UserId!.Value, req.Page, req.PageSize, req.Type), ct)).ToPagedActionResult();
}

public record LoyaltyTransactionsRequest(int Page = 1, int PageSize = 20, string? Type = null);
