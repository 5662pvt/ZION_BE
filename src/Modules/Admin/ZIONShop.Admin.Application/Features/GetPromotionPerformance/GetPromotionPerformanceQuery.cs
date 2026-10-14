using MediatR;
using ZIONShop.Admin.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.GetPromotionPerformance;

public record GetPromotionPerformanceQuery(string? From, string? To) : IRequest<Result<IReadOnlyList<PromotionPerformanceDto>>>;
