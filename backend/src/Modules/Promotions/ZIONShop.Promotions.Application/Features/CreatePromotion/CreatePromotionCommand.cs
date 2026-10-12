using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.CreatePromotion;

public record CreatePromotionCommand(
    string Name,
    string? Description,
    string Type,
    decimal Value,
    decimal? MaxDiscountAmount,
    decimal MinOrderSubtotal,
    DateTime StartsAt,
    DateTime EndsAt,
    int? UsageLimit,
    int? PerUserLimit,
    string Scope = "Order",
    IReadOnlyList<Guid>? TargetProductIds = null,
    IReadOnlyList<Guid>? TargetCategoryIds = null) : IRequest<Result<PromotionDto>>;
