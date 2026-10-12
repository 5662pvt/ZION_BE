using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.SharedKernel.Entities;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Domain.Entities;

public class Promotion : AggregateRoot
{
    private Promotion() { }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public PromotionType Type { get; private set; }
    public decimal Value { get; private set; }
    public decimal? MaxDiscountAmount { get; private set; }
    public decimal MinOrderSubtotal { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public bool IsActive { get; private set; }
    public int? UsageLimit { get; private set; }
    public int? PerUserLimit { get; private set; }
    public int UsedCount { get; private set; }
    public PromotionScope Scope { get; private set; } = PromotionScope.Order;
    public Guid[] TargetProductIds { get; private set; } = Array.Empty<Guid>();
    public Guid[] TargetCategoryIds { get; private set; } = Array.Empty<Guid>();

    public static Promotion Create(
        string name,
        string? description,
        PromotionType type,
        decimal value,
        decimal? maxDiscountAmount,
        decimal minOrderSubtotal,
        DateTime startsAt,
        DateTime endsAt,
        int? usageLimit,
        int? perUserLimit,
        PromotionScope scope = PromotionScope.Order,
        IReadOnlyList<Guid>? targetProductIds = null,
        IReadOnlyList<Guid>? targetCategoryIds = null)
    {
        return new Promotion
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Type = type,
            Value = value,
            MaxDiscountAmount = maxDiscountAmount,
            MinOrderSubtotal = minOrderSubtotal,
            StartsAt = startsAt,
            EndsAt = endsAt,
            UsageLimit = usageLimit,
            PerUserLimit = perUserLimit,
            IsActive = false,
            UsedCount = 0,
            Scope = scope,
            TargetProductIds = targetProductIds?.ToArray() ?? Array.Empty<Guid>(),
            TargetCategoryIds = targetCategoryIds?.ToArray() ?? Array.Empty<Guid>()
        };
    }

    public Result Update(
        string name,
        string? description,
        PromotionType type,
        decimal value,
        decimal? maxDiscountAmount,
        decimal minOrderSubtotal,
        DateTime startsAt,
        DateTime endsAt,
        int? usageLimit,
        int? perUserLimit,
        PromotionScope scope = PromotionScope.Order,
        IReadOnlyList<Guid>? targetProductIds = null,
        IReadOnlyList<Guid>? targetCategoryIds = null)
    {
        if (usageLimit.HasValue && usageLimit.Value < UsedCount)
            return Result.Failure(PromotionsErrors.UsageLimitBelowUsedCount);

        Name = name;
        Description = description;
        Type = type;
        Value = value;
        MaxDiscountAmount = maxDiscountAmount;
        MinOrderSubtotal = minOrderSubtotal;
        StartsAt = startsAt;
        EndsAt = endsAt;
        UsageLimit = usageLimit;
        PerUserLimit = perUserLimit;
        Scope = scope;
        TargetProductIds = targetProductIds?.ToArray() ?? Array.Empty<Guid>();
        TargetCategoryIds = targetCategoryIds?.ToArray() ?? Array.Empty<Guid>();
        return Result.Success();
    }

    /// <summary>Pure containment check against Scope/targets; Categories needs the caller to resolve a productId's CategoryId first (I/O lives in the evaluator).</summary>
    public bool IsLineEligible(Guid productId, Guid? categoryId) => Scope switch
    {
        PromotionScope.Products => TargetProductIds.Contains(productId),
        PromotionScope.Categories => categoryId.HasValue && TargetCategoryIds.Contains(categoryId.Value),
        _ => true
    };

    public Result Activate()
    {
        if (IsActive) return Result.Failure(PromotionsErrors.AlreadyActive);
        IsActive = true;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive) return Result.Failure(PromotionsErrors.AlreadyInactive);
        IsActive = false;
        return Result.Success();
    }

    public PromotionStatus GetStatus(DateTime utcNow)
    {
        if (!IsActive) return PromotionStatus.Inactive;
        if (utcNow < StartsAt) return PromotionStatus.Scheduled;
        if (utcNow > EndsAt) return PromotionStatus.Expired;
        return PromotionStatus.Active;
    }

    /// <summary>Percentage: round(subtotal * value / 100) capped by MaxDiscountAmount. Fixed: min(value, subtotal).</summary>
    public decimal CalculateDiscount(decimal subtotal)
    {
        decimal discount;
        if (Type == PromotionType.PercentageOff)
        {
            discount = Math.Round(subtotal * Value / 100m, 0, MidpointRounding.AwayFromZero);
            if (MaxDiscountAmount.HasValue) discount = Math.Min(discount, MaxDiscountAmount.Value);
        }
        else
        {
            discount = Value;
        }

        return Math.Min(discount, subtotal);
    }

    public void IncrementUsage() => UsedCount++;

    public void DecrementUsage() => UsedCount = Math.Max(0, UsedCount - 1);
}
