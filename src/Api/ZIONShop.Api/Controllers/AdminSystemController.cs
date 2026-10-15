using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ZIONShop.Api.Health;
using ZIONShop.Auth.Authorization;
using ZIONShop.Common.Api;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/admin/system")]
[Authorize(Policy = Policies.AdminOnly)]
public class AdminSystemController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public AdminSystemController(HealthCheckService healthCheckService) => _healthCheckService = healthCheckService;

    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var report = await _healthCheckService.CheckHealthAsync(check => check.Tags.Contains("ready"), ct);
        var dto = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checkedAt = DateTime.UtcNow,
            checks = report.ToChecks()
        };
        return Ok(ApiResponse<object>.Ok(dto));
    }
}
