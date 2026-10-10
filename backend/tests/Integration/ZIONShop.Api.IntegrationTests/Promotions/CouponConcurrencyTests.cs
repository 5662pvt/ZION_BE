using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Promotions;
using static ZIONShop.Api.IntegrationTests.Promotions.PromotionsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Promotions;

[Collection(ApiTestCollection.Name)]
public class CouponConcurrencyTests
{
    private readonly ApiFactory _factory;

    public CouponConcurrencyTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    [Fact]
    public async Task ReserveAsync_Should_AllowExactlyOneWinner_When_TwoOrdersRaceForTheLastUsage()
    {
        var admin = await AdminClientAsync();
        var promotion = await CreatePromotionAsync(admin, type: "FixedAmountOff", value: 10000m, usageLimit: 1);
        var coupon = await CreateCouponAsync(admin, promotion.Id, "RACE1");
        await ActivatePromotionAsync(admin, promotion.Id);

        // Two separate scopes so each gets its own PromotionsDbContext -- mirrors two independent
        // concurrent HTTP requests racing the same promotion row's xmin token for real against Postgres.
        using var scopeA = _factory.Services.CreateScope();
        using var scopeB = _factory.Services.CreateScope();
        var evaluatorA = scopeA.ServiceProvider.GetRequiredService<IPromotionEvaluator>();
        var evaluatorB = scopeB.ServiceProvider.GetRequiredService<IPromotionEvaluator>();

        var orderA = Guid.NewGuid();
        var orderB = Guid.NewGuid();

        var results = await Task.WhenAll(
            evaluatorA.ReserveAsync(coupon.Code, Guid.NewGuid(), orderA, 100000m),
            evaluatorB.ReserveAsync(coupon.Code, Guid.NewGuid(), orderB, 100000m));

        results.Count(r => r.Error is null).Should().Be(1);
        results.Count(r => r.Error is not null).Should().Be(1);
        results.First(r => r.Error is not null).Error!.Message.Should().Be("Mã giảm giá đã hết lượt sử dụng");
    }
}
