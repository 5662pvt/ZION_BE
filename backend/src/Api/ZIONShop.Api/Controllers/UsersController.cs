using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;
using ZIONShop.Users.Application.Features.AddAddress;
using ZIONShop.Users.Application.Features.GetProfile;
using ZIONShop.Users.Application.Features.ListAddresses;
using ZIONShop.Users.Application.Features.RemoveAddress;
using ZIONShop.Users.Application.Features.SetDefaultAddress;
using ZIONShop.Users.Application.Features.UpdateAddress;
using ZIONShop.Users.Application.Features.UpdateProfile;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public UsersController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        return (await _mediator.Send(new GetUserProfileQuery(_currentUser.UserId.Value), ct)).ToActionResult();
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest body, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        var cmd = new UpdateUserProfileCommand(_currentUser.UserId.Value, body.FullName, body.PhoneNumber, body.DateOfBirth);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Profile updated");
    }

    [HttpGet("me/addresses")]
    public async Task<IActionResult> ListAddresses(CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        return (await _mediator.Send(new ListAddressesQuery(_currentUser.UserId.Value), ct)).ToActionResult();
    }

    [HttpPost("me/addresses")]
    public async Task<IActionResult> AddAddress([FromBody] AddAddressRequest body, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        var cmd = new AddAddressCommand(_currentUser.UserId.Value, body.Line1, body.Line2, body.City, body.State, body.Country, body.PostalCode, body.IsDefault);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Address added");
    }

    [HttpPut("me/addresses/{addressId:guid}")]
    public async Task<IActionResult> UpdateAddress(Guid addressId, [FromBody] UpdateAddressRequest body, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        var cmd = new UpdateAddressCommand(_currentUser.UserId.Value, addressId, body.Line1, body.Line2, body.City, body.State, body.Country, body.PostalCode, body.IsDefault);
        return (await _mediator.Send(cmd, ct)).ToActionResult("Address updated");
    }

    [HttpPost("me/addresses/{addressId:guid}/default")]
    public async Task<IActionResult> SetDefaultAddress(Guid addressId, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        return (await _mediator.Send(new SetDefaultAddressCommand(_currentUser.UserId.Value, addressId), ct)).ToActionResult("Default address updated");
    }

    [HttpDelete("me/addresses/{addressId:guid}")]
    public async Task<IActionResult> RemoveAddress(Guid addressId, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Unauthorized(ApiResponse.Fail("Unauthorized"));
        return (await _mediator.Send(new RemoveAddressCommand(_currentUser.UserId.Value, addressId), ct)).ToActionResult("Address removed");
    }
}

public record UpdateProfileRequest(string? FullName, string? PhoneNumber, DateTime? DateOfBirth);
public record AddAddressRequest(string Line1, string? Line2, string City, string? State, string Country, string PostalCode, bool IsDefault);
public record UpdateAddressRequest(string Line1, string? Line2, string City, string? State, string Country, string PostalCode, bool IsDefault);
