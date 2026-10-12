using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Application.Mappings;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.CreatePromotion;

public class CreatePromotionCommandHandler : IRequestHandler<CreatePromotionCommand, Result<PromotionDto>>
{
    private readonly IPromotionRepository _promotions;
    private readonly IPromotionsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public CreatePromotionCommandHandler(IPromotionRepository promotions, IPromotionsUnitOfWork uow, TimeProvider timeProvider)
    {
        _promotions = promotions;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PromotionDto>> Handle(CreatePromotionCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PromotionType>(request.Type, ignoreCase: true, out var type))
            return Result.Failure<PromotionDto>(PromotionsErrors.InvalidPromotionType);

        if (!Enum.TryParse<PromotionScope>(request.Scope, ignoreCase: true, out var scope))
            return Result.Failure<PromotionDto>(PromotionsErrors.InvalidPromotionScope);

        var value = type == PromotionType.FreeShipping ? 0m : request.Value;
        var maxDiscountAmount = type == PromotionType.FreeShipping ? null : request.MaxDiscountAmount;

        var promotion = Promotion.Create(
            request.Name, request.Description, type, value, maxDiscountAmount,
            request.MinOrderSubtotal, request.StartsAt, request.EndsAt, request.UsageLimit, request.PerUserLimit,
            scope, request.TargetProductIds, request.TargetCategoryIds);

        await _promotions.AddAsync(promotion, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return Result.Success(promotion.ToDto(Array.Empty<CouponCode>(), now));
    }
}
