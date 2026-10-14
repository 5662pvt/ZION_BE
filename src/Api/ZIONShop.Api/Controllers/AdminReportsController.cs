using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZIONShop.Admin.Application.Features.ExportPromotionsCsv;
using ZIONShop.Admin.Application.Features.ExportRevenueCsv;
using ZIONShop.Admin.Application.Features.ExportTopProductsCsv;
using ZIONShop.Admin.Application.Features.GetDashboard;
using ZIONShop.Admin.Application.Features.GetPromotionPerformance;
using ZIONShop.Admin.Application.Features.GetTopCategories;
using ZIONShop.Admin.Application.Features.GetTopProducts;
using ZIONShop.Auth.Authorization;
using ZIONShop.Common.Api;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(Policy = Policies.AdminOnly)]
public class AdminReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminReportsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] AdminReportRangeRequest req, CancellationToken ct)
        => (await _mediator.Send(new GetDashboardQuery(req.From, req.To), ct)).ToActionResult();

    [HttpGet("top-products")]
    public async Task<IActionResult> TopProducts([FromQuery] AdminTopProductsRequest req, CancellationToken ct)
        => (await _mediator.Send(new GetTopProductsQuery(req.From, req.To, req.Limit, req.SortBy), ct)).ToActionResult();

    [HttpGet("top-categories")]
    public async Task<IActionResult> TopCategories([FromQuery] AdminTopCategoriesRequest req, CancellationToken ct)
        => (await _mediator.Send(new GetTopCategoriesQuery(req.From, req.To, req.SortBy), ct)).ToActionResult();

    [HttpGet("promotions")]
    public async Task<IActionResult> Promotions([FromQuery] AdminReportRangeRequest req, CancellationToken ct)
        => (await _mediator.Send(new GetPromotionPerformanceQuery(req.From, req.To), ct)).ToActionResult();

    [HttpGet("export/revenue.csv")]
    public async Task<IActionResult> ExportRevenueCsv([FromQuery] AdminReportRangeRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportRevenueCsvQuery(req.From, req.To), ct);
        return result.IsFailure ? result.ToActionResult() : File(result.Value.Content, "text/csv; charset=utf-8", result.Value.FileName);
    }

    [HttpGet("export/top-products.csv")]
    public async Task<IActionResult> ExportTopProductsCsv([FromQuery] AdminReportRangeRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportTopProductsCsvQuery(req.From, req.To), ct);
        return result.IsFailure ? result.ToActionResult() : File(result.Value.Content, "text/csv; charset=utf-8", result.Value.FileName);
    }

    [HttpGet("export/promotions.csv")]
    public async Task<IActionResult> ExportPromotionsCsv([FromQuery] AdminReportRangeRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportPromotionsCsvQuery(req.From, req.To), ct);
        return result.IsFailure ? result.ToActionResult() : File(result.Value.Content, "text/csv; charset=utf-8", result.Value.FileName);
    }
}

public record AdminReportRangeRequest(string? From = null, string? To = null);
public record AdminTopProductsRequest(string? From = null, string? To = null, int Limit = 10, string? SortBy = "revenue");
public record AdminTopCategoriesRequest(string? From = null, string? To = null, string? SortBy = "revenue");
