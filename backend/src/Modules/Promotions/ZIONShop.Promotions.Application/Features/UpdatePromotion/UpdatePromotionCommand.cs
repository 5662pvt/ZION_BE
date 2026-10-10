using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.UpdatePromotion;

public record UpdatePromotionCommand(
    Guid PromotionId,
    string Name,
    string? Description,
    string Type,
    decimal Value,
    decimal? MaxDiscountAmount,
    decimal MinOrderSubtotal,
    DateTime StartsAt,
    DateTime EndsAt,
    int? UsageLimit,
    int? PerUserLimit) : IRequest<Result<PromotionDto>>;
