using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Infrastructure.Persistence;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.FlashSales.FlashSaleTestHelpers;

namespace ZIONShop.Api.IntegrationTests.FlashSales;

/// <summary>
/// Same rationale and structure as <c>ZIONShop.Api.IntegrationTests.Checkout.OversellTests</c>, but for
/// <c>IFlashSaleItemRepository.TryReserveAsync</c>'s atomic conditional UPDATE (SoldQuantity + quantity
/// &lt;= AllocatedQuantity) instead of Inventory's. Drives <c>IFlashSaleEvaluator.ReserveAsync</c> directly
/// (the exact call PlaceOrderCommandHandler makes), each invocation resolved in its own DI scope (own
/// DbContext/connection) and released from a shared start gate at the same time, so the reservation layer
/// is actually exercised under concurrency against the real Postgres testcontainer.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class FlashSaleOversellTests
{
    private readonly ApiFactory _factory;

    public FlashSaleOversellTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    [Fact]
    public async Task FiftyConcurrentReserveCalls_Should_ReserveExactlyTen_When_AllocatedQuantityIsTen()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        var (_, item) = await CreateActiveFlashSaleWithItemAsync(admin, productId, flashPrice: 50000m, allocatedQuantity: 10);

        var attempts = Enumerable.Range(0, 50).Select(_ => (OrderId: Guid.NewGuid(), UserId: Guid.NewGuid())).ToList();
        var startGate = new SemaphoreSlim(0);

        var tasks = attempts.Select(async attempt =>
        {
            using var scope = _factory.Services.CreateScope();
            var evaluator = scope.ServiceProvider.GetRequiredService<IFlashSaleEvaluator>();

            await startGate.WaitAsync();
            return await evaluator.ReserveAsync(new[] { new FlashSaleLine(productId, 1) }, attempt.OrderId, attempt.UserId);
        }).ToList();

        await Task.Delay(50); // give every task a chance to reach the gate before releasing it
        startGate.Release(attempts.Count);

        var results = await Task.WhenAll(tasks); // no exception is part of the assertion

        var successCount = results.Count(r => r.Count > 0);
        successCount.Should().Be(10, "10 units of allocation can satisfy exactly 10 of the 50 single-unit reservation attempts");

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        var reloaded = await db.FlashSaleItems.AsNoTracking().SingleAsync(i => i.Id == item.Id);
        reloaded.SoldQuantity.Should().Be(10);
        reloaded.SoldQuantity.Should().BeLessThanOrEqualTo(reloaded.AllocatedQuantity, "SoldQuantity must never exceed AllocatedQuantity");

        var redemptionCount = await db.FlashSaleRedemptions.CountAsync(r => r.FlashSaleItemId == item.Id);
        redemptionCount.Should().Be(10);
    }
}
