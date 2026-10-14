using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;

namespace ZIONShop.Promotions.Tests.Domain;

public class PromotionTests
{
    private static Promotion CreatePercentage(decimal value, decimal? cap, DateTime startsAt, DateTime endsAt) =>
        Promotion.Create("Sale", null, PromotionType.PercentageOff, value, cap, 0m, startsAt, endsAt, null, null).Value;

    private static Promotion CreateFixed(decimal value, DateTime startsAt, DateTime endsAt) =>
        Promotion.Create("Sale", null, PromotionType.FixedAmountOff, value, null, 0m, startsAt, endsAt, null, null).Value;

    [Fact]
    public void CalculateDiscount_Should_RoundAndCap_When_PercentageOffExceedsMaxDiscount()
    {
        var promotion = CreatePercentage(10m, 15000m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        // 10% of 200,001 = 20,000.1 -> rounds to 20,000 -> capped at 15,000
        var discount = promotion.CalculateDiscount(200001m);

        discount.Should().Be(15000m);
    }

    [Fact]
    public void CalculateDiscount_Should_RoundAwayFromZero_When_PercentageOffHasFraction()
    {
        var promotion = CreatePercentage(10m, null, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        // 10% of 125 = 12.5 -> AwayFromZero rounds to 13
        var discount = promotion.CalculateDiscount(125m);

        discount.Should().Be(13m);
    }

    [Fact]
    public void CalculateDiscount_Should_ReturnSubtotal_When_FixedAmountOffExceedsSubtotal()
    {
        var promotion = CreateFixed(50000m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        var discount = promotion.CalculateDiscount(30000m);

        discount.Should().Be(30000m);
    }

    [Fact]
    public void CalculateDiscount_Should_ReturnFixedValue_When_SubtotalExceedsValue()
    {
        var promotion = CreateFixed(20000m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        var discount = promotion.CalculateDiscount(100000m);

        discount.Should().Be(20000m);
    }

    [Fact]
    public void GetStatus_Should_ReturnInactive_When_NotActivated()
    {
        var promotion = CreateFixed(1000m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        promotion.GetStatus(DateTime.UtcNow).Should().Be(PromotionStatus.Inactive);
    }

    [Fact]
    public void GetStatus_Should_ReturnScheduled_When_ActiveButBeforeStart()
    {
        var promotion = CreateFixed(1000m, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(5));
        promotion.Activate();

        promotion.GetStatus(DateTime.UtcNow).Should().Be(PromotionStatus.Scheduled);
    }

    [Fact]
    public void GetStatus_Should_ReturnExpired_When_ActiveButPastEnd()
    {
        var promotion = CreateFixed(1000m, DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddDays(-1));
        promotion.Activate();

        promotion.GetStatus(DateTime.UtcNow).Should().Be(PromotionStatus.Expired);
    }

    [Fact]
    public void GetStatus_Should_ReturnActive_When_ActivatedWithinWindow()
    {
        var promotion = CreateFixed(1000m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        promotion.Activate();

        promotion.GetStatus(DateTime.UtcNow).Should().Be(PromotionStatus.Active);
    }

    [Fact]
    public void Activate_Should_Fail_When_AlreadyActive()
    {
        var promotion = CreateFixed(1000m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        promotion.Activate();

        var result = promotion.Activate();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.AlreadyActive");
    }

    [Fact]
    public void Deactivate_Should_Fail_When_AlreadyInactive()
    {
        var promotion = CreateFixed(1000m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        var result = promotion.Deactivate();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.AlreadyInactive");
    }

    [Fact]
    public void Update_Should_Fail_When_UsageLimitBelowUsedCount()
    {
        var promotion = CreateFixed(1000m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        promotion.IncrementUsage();
        promotion.IncrementUsage();

        var result = promotion.Update("Sale", null, PromotionType.FixedAmountOff, 1000m, null, 0m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), usageLimit: 1, perUserLimit: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.UsageLimitBelowUsedCount");
    }

    [Fact]
    public void DecrementUsage_Should_NotGoBelowZero()
    {
        var promotion = CreateFixed(1000m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        promotion.DecrementUsage();

        promotion.UsedCount.Should().Be(0);
    }

    [Fact]
    public void Create_Should_DefaultToOrderScope_When_ScopeNotSpecified()
    {
        var promotion = CreateFixed(1000m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        promotion.Scope.Should().Be(PromotionScope.Order);
        promotion.TargetProductIds.Should().BeEmpty();
        promotion.TargetCategoryIds.Should().BeEmpty();
    }

    [Fact]
    public void IsLineEligible_Should_ReturnTrue_When_ScopeIsOrder()
    {
        var promotion = CreateFixed(1000m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        promotion.IsLineEligible(Guid.NewGuid(), null).Should().BeTrue();
    }

    [Fact]
    public void IsLineEligible_Should_ReturnTrue_When_ScopeIsProductsAndProductIdMatches()
    {
        var productId = Guid.NewGuid();
        var promotion = Promotion.Create("Sale", null, PromotionType.FixedAmountOff, 1000m, null, 0m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), null, null,
            PromotionScope.Products, new[] { productId }).Value;

        promotion.IsLineEligible(productId, null).Should().BeTrue();
        promotion.IsLineEligible(Guid.NewGuid(), null).Should().BeFalse();
    }

    [Fact]
    public void IsLineEligible_Should_ReturnTrue_When_ScopeIsCategoriesAndCategoryIdMatches()
    {
        var categoryId = Guid.NewGuid();
        var promotion = Promotion.Create("Sale", null, PromotionType.FixedAmountOff, 1000m, null, 0m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), null, null,
            PromotionScope.Categories, targetCategoryIds: new[] { categoryId }).Value;

        promotion.IsLineEligible(Guid.NewGuid(), categoryId).Should().BeTrue();
        promotion.IsLineEligible(Guid.NewGuid(), Guid.NewGuid()).Should().BeFalse();
        promotion.IsLineEligible(Guid.NewGuid(), null).Should().BeFalse();
    }

    [Fact]
    public void Update_Should_ReplaceScopeAndTargets()
    {
        var promotion = CreateFixed(1000m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        var categoryId = Guid.NewGuid();

        var result = promotion.Update("Sale", null, PromotionType.FixedAmountOff, 1000m, null, 0m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), null, null,
            PromotionScope.Categories, targetCategoryIds: new[] { categoryId });

        result.IsSuccess.Should().BeTrue();
        promotion.Scope.Should().Be(PromotionScope.Categories);
        promotion.TargetCategoryIds.Should().ContainSingle().Which.Should().Be(categoryId);
    }
}
