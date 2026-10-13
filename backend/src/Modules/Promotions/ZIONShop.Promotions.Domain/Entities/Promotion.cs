using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.SharedKernel.Entities;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Domain.Entities;

/// <summary>Input for a gift reward line; validated against <see cref="Promotion.Create"/>/<see cref="Promotion.Update"/>'s gift rules, not persisted itself (see <see cref="PromotionGiftItem"/>).</summary>
public sealed record GiftItemInput(Guid ProductId, int Quantity);

public class Promotion : AggregateRoot
{
    private readonly List<PromotionGiftItem> _giftItems = new();

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
    public int? MinQuantity { get; private set; }
    public IReadOnlyList<PromotionGiftItem> GiftItems => _giftItems.AsReadOnly();

    public static Result<Promotion> Create(
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
        IReadOnlyList<Guid>? targetCategoryIds = null,
        int? minQuantity = null,
        IReadOnlyList<GiftItemInput>? giftItems = null)
    {
        var giftError = ValidateGiftRules(type, scope, minQuantity, minOrderSubtotal, giftItems);
        if (giftError is not null) return Result.Failure<Promotion>(giftError);

        var promotion = new Promotion
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
            TargetCategoryIds = targetCategoryIds?.ToArray() ?? Array.Empty<Guid>(),
            MinQuantity = type == PromotionType.Gift ? minQuantity : null
        };

        if (type == PromotionType.Gift && giftItems is not null)
        {
            foreach (var item in giftItems)
                promotion._giftItems.Add(PromotionGiftItem.Create(promotion.Id, item.ProductId, item.Quantity));
        }

        return Result.Success(promotion);
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
        IReadOnlyList<Guid>? targetCategoryIds = null,
        int? minQuantity = null,
        IReadOnlyList<GiftItemInput>? giftItems = null)
    {
        if (usageLimit.HasValue && usageLimit.Value < UsedCount)
            return Result.Failure(PromotionsErrors.UsageLimitBelowUsedCount);

        var giftError = ValidateGiftRules(type, scope, minQuantity, minOrderSubtotal, giftItems);
        if (giftError is not null) return Result.Failure(giftError);

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
        MinQuantity = type == PromotionType.Gift ? minQuantity : null;

        _giftItems.Clear();
        if (type == PromotionType.Gift && giftItems is not null)
        {
            foreach (var item in giftItems)
                _giftItems.Add(PromotionGiftItem.Create(Id, item.ProductId, item.Quantity));
        }

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

    /// <summary>Structural rules from the Gift contract: non-Gift promotions must not carry gift fields; Gift
    /// promotions must have 1-5 distinct gift items with quantity 1-10, and minQuantity consistent with Scope
    /// (required and ≥1 for Products/Categories, forbidden for Order where MinOrderSubtotal must be &gt; 0 instead).</summary>
    private static Error? ValidateGiftRules(PromotionType type, PromotionScope scope, int? minQuantity, decimal minOrderSubtotal, IReadOnlyList<GiftItemInput>? giftItems)
    {
        if (type != PromotionType.Gift)
        {
            if (giftItems is { Count: > 0 }) return PromotionsErrors.GiftItemsNotAllowed;
            if (minQuantity.HasValue) return PromotionsErrors.MinQuantityNotAllowed;
            return null;
        }

        if (giftItems is null || giftItems.Count == 0) return PromotionsErrors.GiftItemsRequired;
        if (giftItems.Count > 5) return PromotionsErrors.GiftItemsTooMany;
        if (giftItems.Select(g => g.ProductId).Distinct().Count() != giftItems.Count) return PromotionsErrors.GiftItemsDuplicateProduct;
        if (giftItems.Any(g => g.Quantity is < 1 or > 10)) return PromotionsErrors.GiftQuantityInvalid;

        if (scope == PromotionScope.Order)
        {
            if (minQuantity.HasValue) return PromotionsErrors.MinQuantityNotAllowedForOrderScope;
            if (minOrderSubtotal <= 0m) return PromotionsErrors.OrderScopeRequiresPositiveSubtotal;
        }
        else if (!minQuantity.HasValue || minQuantity.Value < 1)
        {
            return PromotionsErrors.MinQuantityRequired;
        }

        return null;
    }
}
