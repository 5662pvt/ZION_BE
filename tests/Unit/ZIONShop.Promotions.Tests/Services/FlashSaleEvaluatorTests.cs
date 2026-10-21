using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Repositories;
using ZIONShop.Promotions.Infrastructure.Services;
using ZIONShop.Promotions.Tests.TestSupport;

namespace ZIONShop.Promotions.Tests.Services;

public class FlashSaleEvaluatorTests
{
    private static (PromotionsDbContext Db, FlashSaleEvaluator Evaluator) BuildScope()
    {
        var options = new DbContextOptionsBuilder<PromotionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new PromotionsDbContext(options);
        var flashSales = new FlashSaleRepository(db);
        var items = new InMemoryFlashSaleItemRepository(db);
        var redemptions = new FlashSaleRedemptionRepository(db);
        var evaluator = new FlashSaleEvaluator(flashSales, items, redemptions, db, TimeProvider.System);
        return (db, evaluator);
    }

    /// <summary>Forces TryReserveAsync to fail for one item id, simulating a lost reservation race on an
    /// otherwise-eligible candidate (quota check passed at selection time, atomic reserve lost the race).</summary>
    private static FlashSaleEvaluator BuildEvaluatorWithFlakyReserve(PromotionsDbContext db, Guid flakyItemId)
    {
        var flashSales = new FlashSaleRepository(db);
        var items = new RaceLosingFlashSaleItemRepository(new InMemoryFlashSaleItemRepository(db), flakyItemId);
        var redemptions = new FlashSaleRedemptionRepository(db);
        return new FlashSaleEvaluator(flashSales, items, redemptions, db, TimeProvider.System);
    }

    private sealed class RaceLosingFlashSaleItemRepository : IFlashSaleItemRepository
    {
        private readonly IFlashSaleItemRepository _inner;
        private readonly Guid _flakyItemId;

        public RaceLosingFlashSaleItemRepository(IFlashSaleItemRepository inner, Guid flakyItemId)
        {
            _inner = inner;
            _flakyItemId = flakyItemId;
        }

        public Task<FlashSaleItem?> GetByIdAsync(Guid id, CancellationToken ct = default) => _inner.GetByIdAsync(id, ct);
        public Task<List<FlashSaleItem>> GetByFlashSaleIdAsync(Guid flashSaleId, CancellationToken ct = default) => _inner.GetByFlashSaleIdAsync(flashSaleId, ct);
        public Task AddAsync(FlashSaleItem item, CancellationToken ct = default) => _inner.AddAsync(item, ct);
        public void Update(FlashSaleItem item) => _inner.Update(item);
        public IQueryable<FlashSaleItem> Query() => _inner.Query();

        public Task<bool> TryReserveAsync(Guid flashSaleItemId, int quantity, CancellationToken ct = default) =>
            flashSaleItemId == _flakyItemId ? Task.FromResult(false) : _inner.TryReserveAsync(flashSaleItemId, quantity, ct);

        public Task ReleaseAsync(Guid flashSaleItemId, int quantity, CancellationToken ct = default) => _inner.ReleaseAsync(flashSaleItemId, quantity, ct);
    }

    private static async Task<FlashSaleItem> SeedActiveFlashSaleItemAsync(
        PromotionsDbContext db, Guid productId, decimal flashPrice, int allocatedQuantity,
        DateTime? startsAt = null, DateTime? endsAt = null, Guid? variantId = null)
    {
        var flashSale = FlashSale.Create("Flash", startsAt ?? DateTime.UtcNow.AddHours(-1), endsAt ?? DateTime.UtcNow.AddHours(1));
        flashSale.Activate();
        db.FlashSales.Add(flashSale);

        var item = FlashSaleItem.Create(flashSale.Id, productId, variantId ?? productId, flashPrice, allocatedQuantity);
        db.FlashSaleItems.Add(item);

        await db.SaveChangesAsync();
        return item;
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnQuote_When_ActiveFlashSaleHasAvailableQuota()
    {
        var (db, evaluator) = BuildScope();
        var productId = Guid.NewGuid();
        var item = await SeedActiveFlashSaleItemAsync(db, productId, 50000m, 10);

        var quotes = await evaluator.QuoteAsync(new[] { new FlashSaleLine(productId, 3) });

        quotes.Should().ContainSingle(q => q.ProductId == productId && q.FlashSaleItemId == item.Id && q.FlashUnitPrice == 50000m);
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnEmpty_When_NoActiveFlashSaleForProduct()
    {
        var (_, evaluator) = BuildScope();

        var quotes = await evaluator.QuoteAsync(new[] { new FlashSaleLine(Guid.NewGuid(), 1) });

        quotes.Should().BeEmpty();
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnEmpty_When_RequestedQuantityExceedsAvailable()
    {
        var (db, evaluator) = BuildScope();
        var productId = Guid.NewGuid();
        await SeedActiveFlashSaleItemAsync(db, productId, 50000m, allocatedQuantity: 2);

        var quotes = await evaluator.QuoteAsync(new[] { new FlashSaleLine(productId, 3) }); // all-or-nothing

        quotes.Should().BeEmpty();
    }

    [Fact]
    public async Task ReserveAsync_Should_ClaimFullQuantity_And_CreateRedemption_When_QuotaAvailable()
    {
        var (db, evaluator) = BuildScope();
        var productId = Guid.NewGuid();
        var item = await SeedActiveFlashSaleItemAsync(db, productId, 50000m, 10);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var quotes = await evaluator.ReserveAsync(new[] { new FlashSaleLine(productId, 4) }, orderId, userId);

        quotes.Should().ContainSingle(q => q.ProductId == productId && q.FlashUnitPrice == 50000m);
        var reloaded = await db.FlashSaleItems.AsNoTracking().FirstAsync(i => i.Id == item.Id);
        reloaded.SoldQuantity.Should().Be(4);
        var redemption = await db.FlashSaleRedemptions.AsNoTracking().SingleAsync(r => r.OrderId == orderId);
        redemption.Status.Should().Be(RedemptionStatus.Reserved);
        redemption.Quantity.Should().Be(4);
        redemption.UnitFlashPrice.Should().Be(50000m);
    }

    [Fact]
    public async Task ReserveAsync_Should_OmitLine_When_QuotaInsufficientForFullQuantity()
    {
        var (db, evaluator) = BuildScope();
        var productId = Guid.NewGuid();
        await SeedActiveFlashSaleItemAsync(db, productId, 50000m, allocatedQuantity: 2);

        var quotes = await evaluator.ReserveAsync(new[] { new FlashSaleLine(productId, 5) }, Guid.NewGuid(), Guid.NewGuid());

        quotes.Should().BeEmpty(); // does not fail the whole order, just doesn't flash-price this line
        (await db.FlashSaleRedemptions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ReserveAsync_Should_ReturnRightSubset_When_QuotaPartiallyExhaustedAcrossLines()
    {
        var (db, evaluator) = BuildScope();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        await SeedActiveFlashSaleItemAsync(db, productA, 50000m, allocatedQuantity: 10); // plenty
        await SeedActiveFlashSaleItemAsync(db, productB, 70000m, allocatedQuantity: 1); // can't cover qty 2

        var quotes = await evaluator.ReserveAsync(
            new[] { new FlashSaleLine(productA, 3), new FlashSaleLine(productB, 2) }, Guid.NewGuid(), Guid.NewGuid());

        quotes.Should().ContainSingle(q => q.ProductId == productA);
        quotes.Should().NotContain(q => q.ProductId == productB);
    }

    [Fact]
    public async Task ReserveAsync_Should_BeIdempotent_When_CalledTwiceForSameOrder()
    {
        var (db, evaluator) = BuildScope();
        var productId = Guid.NewGuid();
        var item = await SeedActiveFlashSaleItemAsync(db, productId, 50000m, 10);
        var orderId = Guid.NewGuid();
        var lines = new[] { new FlashSaleLine(productId, 4) };

        await evaluator.ReserveAsync(lines, orderId, Guid.NewGuid());
        await evaluator.ReserveAsync(lines, orderId, Guid.NewGuid());

        var reloaded = await db.FlashSaleItems.AsNoTracking().FirstAsync(i => i.Id == item.Id);
        reloaded.SoldQuantity.Should().Be(4); // not double-reserved
        (await db.FlashSaleRedemptions.CountAsync(r => r.OrderId == orderId)).Should().Be(1);
    }

    [Fact]
    public async Task ReserveAsync_Should_ReturnExistingRedemption_When_RetryHappensAfterQuotaExhaustedByOtherOrders()
    {
        var (db, evaluator) = BuildScope();
        var productId = Guid.NewGuid();
        var item = await SeedActiveFlashSaleItemAsync(db, productId, 50000m, allocatedQuantity: 5);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var line = new FlashSaleLine(productId, 4);

        var firstQuotes = await evaluator.ReserveAsync(new[] { line }, orderId, userId);
        firstQuotes.Should().ContainSingle(q => q.FlashSaleItemId == item.Id);

        // A different order consumes the remaining quota in between, so "candidates with spare quota right
        // now" no longer includes this item at all.
        var items = new InMemoryFlashSaleItemRepository(db);
        (await items.TryReserveAsync(item.Id, 1, CancellationToken.None)).Should().BeTrue();

        // A retry for the SAME order/line (e.g. an idempotency replay) must still find its own existing
        // redemption instead of dropping the line or attempting a second reservation.
        var retryQuotes = await evaluator.ReserveAsync(new[] { line }, orderId, userId);

        retryQuotes.Should().ContainSingle(q => q.FlashSaleItemId == item.Id && q.FlashUnitPrice == 50000m);
        var reloaded = await db.FlashSaleItems.AsNoTracking().FirstAsync(i => i.Id == item.Id);
        reloaded.SoldQuantity.Should().Be(5, "the retry must not attempt a second reservation for the same order");
        (await db.FlashSaleRedemptions.CountAsync(r => r.OrderId == orderId)).Should().Be(1);
    }

    [Fact]
    public async Task ReleaseAsync_Should_DecrementSoldQuantity_And_MarkReleased()
    {
        var (db, evaluator) = BuildScope();
        var productId = Guid.NewGuid();
        var item = await SeedActiveFlashSaleItemAsync(db, productId, 50000m, 10);
        var orderId = Guid.NewGuid();
        await evaluator.ReserveAsync(new[] { new FlashSaleLine(productId, 4) }, orderId, Guid.NewGuid());

        await evaluator.ReleaseAsync(orderId);

        var reloaded = await db.FlashSaleItems.AsNoTracking().FirstAsync(i => i.Id == item.Id);
        reloaded.SoldQuantity.Should().Be(0);
        var redemption = await db.FlashSaleRedemptions.AsNoTracking().SingleAsync(r => r.OrderId == orderId);
        redemption.Status.Should().Be(RedemptionStatus.Released);
    }

    [Fact]
    public async Task ReleaseAsync_Should_BeNoOp_When_NothingWasReserved()
    {
        var (_, evaluator) = BuildScope();

        await evaluator.ReleaseAsync(Guid.NewGuid()); // must not throw
    }

    [Fact]
    public async Task QuoteAsync_Should_MatchByVariant_When_SameProductHasTwoFlashPricedVariants()
    {
        var (db, evaluator) = BuildScope();
        var productId = Guid.NewGuid();
        var variantA = Guid.NewGuid();
        var variantB = Guid.NewGuid();
        var itemA = await SeedActiveFlashSaleItemAsync(db, productId, 40000m, 5, variantId: variantA);
        await SeedActiveFlashSaleItemAsync(db, productId, 60000m, 5, variantId: variantB);

        var quotes = await evaluator.QuoteAsync(new[] { new FlashSaleLine(productId, 1, variantA) });

        quotes.Should().ContainSingle(q => q.FlashSaleItemId == itemA.Id && q.FlashUnitPrice == 40000m && q.VariantId == variantA);
    }

    [Fact]
    public async Task ReserveAsync_Should_MoveOnlyThatVariantsQuota_When_SameProductHasTwoFlashPricedVariants()
    {
        var (db, evaluator) = BuildScope();
        var productId = Guid.NewGuid();
        var variantA = Guid.NewGuid();
        var variantB = Guid.NewGuid();
        var itemA = await SeedActiveFlashSaleItemAsync(db, productId, 40000m, 5, variantId: variantA);
        var itemB = await SeedActiveFlashSaleItemAsync(db, productId, 60000m, 5, variantId: variantB);

        var quotes = await evaluator.ReserveAsync(new[] { new FlashSaleLine(productId, 2, variantA) }, Guid.NewGuid(), Guid.NewGuid());

        quotes.Should().ContainSingle(q => q.VariantId == variantA && q.FlashSaleItemId == itemA.Id);
        var reloadedA = await db.FlashSaleItems.AsNoTracking().FirstAsync(i => i.Id == itemA.Id);
        var reloadedB = await db.FlashSaleItems.AsNoTracking().FirstAsync(i => i.Id == itemB.Id);
        reloadedA.SoldQuantity.Should().Be(2);
        reloadedB.SoldQuantity.Should().Be(0, "only the reserved variant's quota should move");
    }

    [Fact]
    public async Task ReserveAsync_Should_FallBackToOtherCampaign_When_CheapestCampaignLacksQuota()
    {
        var (db, evaluator) = BuildScope();
        var productId = Guid.NewGuid();
        var itemA = await SeedActiveFlashSaleItemAsync(db, productId, 40000m, allocatedQuantity: 2); // cheaper but not enough quota
        var itemB = await SeedActiveFlashSaleItemAsync(db, productId, 60000m, allocatedQuantity: 5); // pricier but enough quota

        var quotes = await evaluator.ReserveAsync(new[] { new FlashSaleLine(productId, 3) }, Guid.NewGuid(), Guid.NewGuid());

        quotes.Should().ContainSingle(q => q.FlashSaleItemId == itemB.Id && q.FlashUnitPrice == 60000m);
        var reloadedA = await db.FlashSaleItems.AsNoTracking().FirstAsync(i => i.Id == itemA.Id);
        var reloadedB = await db.FlashSaleItems.AsNoTracking().FirstAsync(i => i.Id == itemB.Id);
        reloadedA.SoldQuantity.Should().Be(0, "the under-quota campaign must not be touched");
        reloadedB.SoldQuantity.Should().Be(3);
    }

    [Fact]
    public async Task ReserveAsync_Should_PickCheapestCampaign_When_BothHaveEnoughQuota()
    {
        var (db, evaluator) = BuildScope();
        var productId = Guid.NewGuid();
        var itemCheap = await SeedActiveFlashSaleItemAsync(db, productId, 40000m, allocatedQuantity: 5);
        await SeedActiveFlashSaleItemAsync(db, productId, 60000m, allocatedQuantity: 5);

        var quotes = await evaluator.ReserveAsync(new[] { new FlashSaleLine(productId, 3) }, Guid.NewGuid(), Guid.NewGuid());

        quotes.Should().ContainSingle(q => q.FlashSaleItemId == itemCheap.Id && q.FlashUnitPrice == 40000m);
    }

    [Fact]
    public async Task ReserveAsync_Should_FallThroughToNextCandidate_When_FirstCandidateLosesReservationRace()
    {
        var (db, _) = BuildScope();
        var productId = Guid.NewGuid();
        var itemA = await SeedActiveFlashSaleItemAsync(db, productId, 40000m, allocatedQuantity: 5); // cheapest, but will lose the reserve race
        var itemB = await SeedActiveFlashSaleItemAsync(db, productId, 60000m, allocatedQuantity: 5);
        var evaluator = BuildEvaluatorWithFlakyReserve(db, itemA.Id);

        var quotes = await evaluator.ReserveAsync(new[] { new FlashSaleLine(productId, 2) }, Guid.NewGuid(), Guid.NewGuid());

        quotes.Should().ContainSingle(q => q.FlashSaleItemId == itemB.Id && q.FlashUnitPrice == 60000m);
        var reloadedA = await db.FlashSaleItems.AsNoTracking().FirstAsync(i => i.Id == itemA.Id);
        var reloadedB = await db.FlashSaleItems.AsNoTracking().FirstAsync(i => i.Id == itemB.Id);
        reloadedA.SoldQuantity.Should().Be(0, "the race-losing candidate must not end up partially reserved");
        reloadedB.SoldQuantity.Should().Be(2);
    }
}
