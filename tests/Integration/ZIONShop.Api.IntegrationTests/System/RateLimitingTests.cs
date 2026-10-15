using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.SystemOps;

/// <summary>
/// Builds a second WebApplicationFactory host on top of the same shared Postgres container, with
/// low Checkout/Search permit limits, instead of lowering them on <see cref="ApiFactory"/> itself --
/// every other test in this collection relies on the shared factory's high defaults (see
/// ApiFactory.ConfigureWebHost) to avoid 429s of its own. Program.cs binds RateLimiting:* into
/// IOptions&lt;RateLimitingOptions&gt; and only reads it from DI inside the limiter partition
/// factories at request time, so a plain ConfigureAppConfiguration override here -- layered on top
/// of ApiFactory's own override via WithWebHostBuilder -- is seen correctly (last-registered
/// configuration source wins), with no process environment variable involved.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RateLimitingTests
{
    private readonly ApiFactory _factory;

    public RateLimitingTests(ApiFactory factory) => _factory = factory;

    private static WebApplicationFactory<Program> CreateLowLimitFactory(ApiFactory factory, int checkoutLimit, int searchLimit)
        => factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:CheckoutPermitLimit"] = checkoutLimit.ToString(),
                ["RateLimiting:SearchPermitLimit"] = searchLimit.ToString(),
            })));

    private static HttpClient CreateLowLimitClient(ApiFactory factory, int checkoutLimit, int searchLimit)
        => CreateLowLimitFactory(factory, checkoutLimit, searchLimit).CreateClient();

    [Fact]
    public async Task Checkout_Should_Return429_OnThirdRequestWithinWindow_And_LeaveOtherEndpointsUnaffected()
    {
        var (sharedClient, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        var accessToken = sharedClient.DefaultRequestHeaders.Authorization!.Parameter!;

        var lowLimitClient = CreateLowLimitClient(_factory, checkoutLimit: 2, searchLimit: 1000);
        lowLimitClient.WithBearerToken(accessToken);

        var first = await PlaceOrderRawAsync(lowLimitClient, addressId, "COD");
        var second = await PlaceOrderRawAsync(lowLimitClient, addressId, "COD");
        var third = await PlaceOrderRawAsync(lowLimitClient, addressId, "COD");

        first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        third.Headers.Should().ContainKey("Retry-After");

        var envelope = await third.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Success.Should().BeFalse();
        envelope.Errors.Should().ContainSingle(e => e.Code == "RateLimit.Exceeded");

        // A different endpoint on the same (throttled) host must not be affected by the
        // checkout-specific policy exceeding its limit.
        var otherEndpoint = await lowLimitClient.GetAsync("/api/v1/orders/mine");
        otherEndpoint.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Checkout_Should_RateLimitPerUser_NotPerSharedIp_When_TwoCustomersBehindSameNat()
    {
        var (customerAShared, addressAId, _) = await CreateCustomerWithAddressAsync(_factory);
        var accessTokenA = customerAShared.DefaultRequestHeaders.Authorization!.Parameter!;

        var (customerBShared, addressBId, _) = await CreateCustomerWithAddressAsync(_factory);
        var accessTokenB = customerBShared.DefaultRequestHeaders.Authorization!.Parameter!;

        // Both clients are built from the SAME low-limit factory/host, so they share the TestServer's
        // "remote IP" (simulating two customers behind one NAT) while carrying different bearer
        // tokens -- this is what distinguishes per-user partitioning from the IP fallback.
        var lowLimitFactory = CreateLowLimitFactory(_factory, checkoutLimit: 2, searchLimit: 1000);
        var clientA = lowLimitFactory.CreateClient().WithBearerToken(accessTokenA);
        var clientB = lowLimitFactory.CreateClient().WithBearerToken(accessTokenB);

        var aFirst = await PlaceOrderRawAsync(clientA, addressAId, "COD");
        var aSecond = await PlaceOrderRawAsync(clientA, addressAId, "COD");
        var bFirst = await PlaceOrderRawAsync(clientB, addressBId, "COD");
        var bSecond = await PlaceOrderRawAsync(clientB, addressBId, "COD");

        aFirst.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        aSecond.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        bFirst.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        bSecond.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);

        var aThird = await PlaceOrderRawAsync(clientA, addressAId, "COD");
        aThird.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Search_Should_Return429_OnThirdRequestWithinWindow()
    {
        var lowLimitClient = CreateLowLimitClient(_factory, checkoutLimit: 1000, searchLimit: 2);

        var first = await lowLimitClient.GetAsync("/api/v1/products?page=1&pageSize=5");
        var second = await lowLimitClient.GetAsync("/api/v1/products?page=1&pageSize=5");
        var third = await lowLimitClient.GetAsync("/api/v1/products?page=1&pageSize=5");

        first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        third.Headers.Should().ContainKey("Retry-After");
    }
}
