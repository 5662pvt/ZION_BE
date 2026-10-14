using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Application.Mappings;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.GenerateCoupons;

public class GenerateCouponsCommandHandler : IRequestHandler<GenerateCouponsCommand, Result<IReadOnlyList<CouponDto>>>
{
    private const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int SuffixLength = 6;
    private const int MaxGenerationRounds = 10;

    private readonly IPromotionRepository _promotions;
    private readonly ICouponCodeRepository _coupons;
    private readonly IPromotionsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public GenerateCouponsCommandHandler(IPromotionRepository promotions, ICouponCodeRepository coupons, IPromotionsUnitOfWork uow, TimeProvider timeProvider)
    {
        _promotions = promotions;
        _coupons = coupons;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task<Result<IReadOnlyList<CouponDto>>> Handle(GenerateCouponsCommand request, CancellationToken cancellationToken)
    {
        var promotion = await _promotions.GetByIdAsync(request.PromotionId, cancellationToken);
        if (promotion is null) return Result.Failure<IReadOnlyList<CouponDto>>(PromotionsErrors.PromotionNotFound);

        if (promotion.Type == PromotionType.Gift)
            return Result.Failure<IReadOnlyList<CouponDto>>(PromotionsErrors.CouponNotAllowedForGift);

        var prefix = request.Prefix.Trim().ToUpperInvariant();
        var codes = new HashSet<string>(StringComparer.Ordinal);

        for (var round = 0; round < MaxGenerationRounds && codes.Count < request.Count; round++)
        {
            var needed = request.Count - codes.Count;
            for (var i = 0; i < needed; i++)
                codes.Add($"{prefix}-{GenerateSuffix()}");

            var existing = await _coupons.GetExistingCodesAsync(codes, cancellationToken);
            codes.ExceptWith(existing);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var coupons = codes.Take(request.Count).Select(code => CouponCode.Create(promotion.Id, code, now)).ToList();

        await _coupons.AddRangeAsync(coupons, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CouponDto>>(coupons.Select(c => c.ToDto()).ToList());
    }

    private static string GenerateSuffix() =>
        new(Enumerable.Range(0, SuffixLength).Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)]).ToArray());
}
