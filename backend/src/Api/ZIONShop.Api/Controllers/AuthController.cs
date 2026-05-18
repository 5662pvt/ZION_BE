using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZIONShop.Auth.Application.Features.Login;
using ZIONShop.Auth.Application.Features.Me;
using ZIONShop.Auth.Application.Features.RefreshToken;
using ZIONShop.Auth.Application.Features.Register;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public AuthController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterUserCommand command, CancellationToken ct)
        => (await _mediator.Send(command, ct)).ToActionResult("Registered");

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
        => (await _mediator.Send(command, ct)).ToActionResult("Logged in");

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command, CancellationToken ct)
        => (await _mediator.Send(command, ct)).ToActionResult("Token refreshed");

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        return (await _mediator.Send(new GetCurrentUserQuery(_currentUser.UserId.Value), ct)).ToActionResult();
    }
}
