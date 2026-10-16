using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Api.Filters;
using ZIONShop.Auth.Application.Features.GetCustomer;
using ZIONShop.Auth.Application.Features.ListCustomers;
using ZIONShop.Auth.Application.Features.LockCustomer;
using ZIONShop.Auth.Application.Features.UnlockCustomer;
using ZIONShop.Auth.Authorization;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/admin/customers")]
[Authorize(Policy = Policies.AdminOnly)]
public class AdminCustomersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public AdminCustomersController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminListCustomersRequest req, CancellationToken ct)
    {
        var query = new ListCustomersQuery(
            req.Page <= 0 ? 1 : req.Page,
            req.PageSize <= 0 ? 20 : req.PageSize,
            req.Keyword,
            req.Role,
            req.Status);
        return (await _mediator.Send(query, ct)).ToPagedActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await _mediator.Send(new GetCustomerQuery(id), ct)).ToActionResult();

    [HttpPost("{id:guid}/lock")]
    [AdminAudit("Customer.Lock", "Customer")]
    public async Task<IActionResult> Lock(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        var cmd = new LockCustomerCommand(id, _currentUser.UserId.Value);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Đã khóa tài khoản khách hàng");
    }

    [HttpPost("{id:guid}/unlock")]
    [AdminAudit("Customer.Unlock", "Customer")]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
        => (await _mediator.Send(new UnlockCustomerCommand(id), ct)).ToActionResult("Đã mở khóa tài khoản khách hàng");
}

public record AdminListCustomersRequest(int Page = 1, int PageSize = 20, string? Keyword = null, string? Role = null, string? Status = null);
