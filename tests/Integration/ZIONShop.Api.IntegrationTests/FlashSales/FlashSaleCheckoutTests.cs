using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Infrastructure.Persistence;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.FlashSales.FlashSaleTestHelpers;

namespace ZIONShop.Api.IntegrationTests.FlashSales;

[Collection(ApiTestCollection.Name)]
public class FlashSaleCheckoutTests
{
    private readonly ApiFactory _factory;

    public FlashSaleCheckoutTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    [Fact]
    public async Task EndToEnd_Should_PriceLineAtFlashPrice_And_KeepQuotaConsumed_When_OrderConfirmed()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 200000m);
        await AdjustStockAsync(admin, productId, 10);

        var (flashSale, _) = await CreateActiveFlashSaleWithItemAsync(admin, productId, flashPrice: 120000m, allocatedQuantity: 5);

        var active = await GetActiveFlashSalesAsync(admin);
        var activeEntry = active.Should().ContainSingle(f => f.Id == flashSale.Id).Subject;
        activeEntry.Items.Should().ContainSingle(i => i.ProductId == productId && i.FlashPrice == 120000m && i.Available == 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 2);
        var order = await PlaceOrderAsync(customer, addressId, "COD");

        order.Lines.Should().ContainSingle(l => l.ProductId == productId && l.UnitPrice == 120000m);
        order.Subtotal.Should().Be(240000m); // 2 * flash price, not 2 * regular price

        await WaitForOrderStatusAsync(customer, order.Id, "Confirmed");

        // OrderConfirmedIntegrationEvent (and the flash-sale commit it triggers in Promotions) flows
        // through the transactional outbox -- same async-after-status-settles timing as
        // CheckoutTestHelpers.WaitForInventoryAsync/WaitForPromotionUsedCountAsync -- so the redemption
        // status must be polled rather than read once right after the order status check.
        var redemption = await WaitForFlashSaleRedemptionStatusAsync(order.Id, "Committed");
        redemption.Status.ToString().Should().Be("Committed");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        var item = await db.FlashSaleItems.AsNoTracking().SingleAsync(i => i.FlashSaleId == flashSale.Id && i.ProductId == productId);
        item.SoldQuantity.Should().Be(2); // quota stays consumed (not released) -- confirms commit, not release
        item.Available.Should().Be(3);
    }

    [Fact]
    public async Task EndToEnd_Should_ReleaseQuota_When_OrderCancelled()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 200000m);
        await AdjustStockAsync(admin, productId, 10);

        var (flashSale, _) = await CreateActiveFlashSaleWithItemAsync(admin, productId, flashPrice: 120000m, allocatedQuantity: 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 2);
        var order = await PlaceOrderAsync(customer, addressId, "COD");

        await WaitForOrderStatusAsync(customer, order.Id, "Confirmed");
        await CancelOrderRawAsync(customer, order.Id, "Changed my mind");
        await WaitForOrderStatusAsync(customer, order.Id, "Cancelled");

        // OrderCancelledIntegrationEvent (and the flash-sale release it triggers in Promotions) flows
        // through the transactional outbox -- it settles after the order status already reports
        // Cancelled, so the quota must be polled rather than read once (same rationale as
        // CheckoutTestHelpers.WaitForInventoryAsync/WaitForPromotionUsedCountAsync).
        var item = await WaitForFlashSaleItemAsync(flashSale.Id, productId, i => i.SoldQuantity == 0);
        item.SoldQuantity.Should().Be(0);
        item.Available.Should().Be(5);
    }

    private async Task<FlashSaleItemData> WaitForFlashSaleItemAsync(
        Guid flashSaleId, Guid productId, Func<FlashSaleItemData, bool> predicate, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        FlashSaleItemData last;
        do
        {
            var entity = await db.FlashSaleItems.AsNoTracking().SingleAsync(i => i.FlashSaleId == flashSaleId && i.ProductId == productId);
            last = new FlashSaleItemData(entity.Id, entity.ProductId, entity.FlashPrice, entity.AllocatedQuantity, entity.SoldQuantity, entity.AllocatedQuantity - entity.SoldQuantity);
            if (predicate(last)) return last;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException($"FlashSaleItem for flashSaleId={flashSaleId} productId={productId} did not reach the expected state within {timeout}. Last observed: SoldQuantity={last.SoldQuantity}, Available={last.Available}.");
    }

    private async Task<FlashSaleRedemption> WaitForFlashSaleRedemptionStatusAsync(
        Guid orderId, string expectedStatus, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        FlashSaleRedemption last;
        do
        {
            last = await db.FlashSaleRedemptions.AsNoTracking().SingleAsync(r => r.OrderId == orderId);
            if (last.Status.ToString() == expectedStatus) return last;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException($"FlashSaleRedemption for orderId={orderId} did not reach status '{expectedStatus}' within {timeout}. Last observed: '{last.Status}'.");
    }
}
