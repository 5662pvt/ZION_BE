using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Api.Filters;
using ZIONShop.Auth.Authorization;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;
using ZIONShop.Loyalty.Application.Features.AdjustLoyaltyPoints;
using ZIONShop.Loyalty.Application.Features.GetCustomerLoyalty;
using ZIONShop.Loyalty.Application.Features.GetCustomerLoyaltyTransactions;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/admin/loyalty")]
[Authorize(Policy = Policies.AdminOnly)]
public class AdminLoyaltyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public AdminLoyaltyController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<IActionResult> GetCustomerLoyalty(Guid userId, CancellationToken ct)
        => (await _mediator.Send(new GetCustomerLoyaltyQuery(userId), ct)).ToActionResult();

    [HttpGet("users/{userId:guid}/transactions")]
    public async Task<IActionResult> GetCustomerLoyaltyTransactions(Guid userId, [FromQuery] LoyaltyTransactionsRequest req, CancellationToken ct)
        => (await _mediator.Send(new GetCustomerLoyaltyTransactionsQuery(userId, req.Page, req.PageSize, req.Type), ct)).ToPagedActionResult();

    [HttpPost("users/{userId:guid}/adjust")]
    [AdminAudit("Loyalty.Adjust", "LoyaltyAccount", RouteKey = "userId")]
    public async Task<IActionResult> Adjust(Guid userId, [FromBody] AdjustLoyaltyPointsBody body, CancellationToken ct)
    {
        var cmd = new AdjustLoyaltyPointsCommand(userId, body.Delta, body.Reason, _currentUser.UserId!.Value);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Đã điều chỉnh số Xu");
    }
}

public record AdjustLoyaltyPointsBody(int Delta, string Reason);
