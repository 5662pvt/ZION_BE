using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZIONShop.Auth.Application.Features.Login;
using ZIONShop.Auth.Application.Features.Me;
using ZIONShop.Auth.Application.Features.RefreshToken;
using ZIONShop.Auth.Application.Features.ForgotPassword;
using ZIONShop.Auth.Application.Features.Register;
using ZIONShop.Auth.Application.Features.ResendVerification;
using ZIONShop.Auth.Application.Features.ResetPassword;
using ZIONShop.Auth.Application.Features.RevokeToken;
using ZIONShop.Auth.Application.Features.VerifyEmail;
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
        => (await _mediator.Send(command, ct)).ToActionResult("Check your email for a verification code");

    [HttpPost("verify-email")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailCommand command, CancellationToken ct)
        => (await _mediator.Send(command, ct)).ToActionResult("Email verified");

    [HttpPost("resend-verification")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationCommand command, CancellationToken ct)
        => (await _mediator.Send(command, ct)).ToActionResult();

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] RequestPasswordResetCommand command, CancellationToken ct)
        => (await _mediator.Send(command, ct)).ToActionResult();

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command, CancellationToken ct)
        => (await _mediator.Send(command, ct)).ToActionResult("Password reset");

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
        => (await _mediator.Send(command, ct)).ToActionResult("Logged in");

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command, CancellationToken ct)
        => (await _mediator.Send(command, ct)).ToActionResult("Token refreshed");

    [HttpPost("revoke")]
    [AllowAnonymous]
    public async Task<IActionResult> Revoke([FromBody] RevokeRefreshTokenCommand command, CancellationToken ct)
        => (await _mediator.Send(command, ct)).ToActionResult("Token revoked");

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        return (await _mediator.Send(new GetCurrentUserQuery(_currentUser.UserId.Value), ct)).ToActionResult();
    }
}
