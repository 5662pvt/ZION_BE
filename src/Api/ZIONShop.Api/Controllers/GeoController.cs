using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Common.Api;
using ZIONShop.Users.Application.Features.GetProvinces;
using ZIONShop.Users.Application.Features.GetWardsByProvince;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/geo")]
public class GeoController : ControllerBase
{
    private readonly IMediator _mediator;

    public GeoController(IMediator mediator) => _mediator = mediator;

    [HttpGet("provinces")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProvinces(CancellationToken ct)
        => (await _mediator.Send(new GetProvincesQuery(), ct)).ToActionResult();

    [HttpGet("provinces/{provinceId:guid}/wards")]
    [AllowAnonymous]
    public async Task<IActionResult> GetWards(Guid provinceId, CancellationToken ct)
        => (await _mediator.Send(new GetWardsByProvinceQuery(provinceId), ct)).ToActionResult();
}
