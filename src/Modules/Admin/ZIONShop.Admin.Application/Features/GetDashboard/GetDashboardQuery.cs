using MediatR;
using ZIONShop.Admin.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.GetDashboard;

public record GetDashboardQuery(string? From, string? To) : IRequest<Result<DashboardReportDto>>;
