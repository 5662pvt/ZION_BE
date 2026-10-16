using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Auth.Infrastructure.Persistence;

namespace ZIONShop.Api.IntegrationTests.Auth;

/// <summary>
/// Builds a second host on top of the same shared Postgres container (same pattern as
/// RateLimitingTests.cs) with Seed:LoadTestCustomers=3, instead of setting it on the shared
/// ApiFactory itself -- every other test in this collection would otherwise pay for 3 extra seeded
/// users it doesn't need.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class SeededTestCustomersTests
{
    private readonly ApiFactory _factory;

    public SeededTestCustomersTests(ApiFactory factory) => _factory = factory;

    private static WebApplicationFactory<Program> CreateLoadTestFactory(ApiFactory factory, int loadTestCustomers)
        => factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:LoadTestCustomers"] = loadTestCustomers.ToString(),
            })));

    [Fact]
    public async Task Seed_Should_CreateConfirmedCustomerAndLoadTestUsers_And_BeIdempotentOnRerun()
    {
        var loadTestFactory = CreateLoadTestFactory(_factory, loadTestCustomers: 3);

        // Force the host (and its startup MigrateAndSeedAsync call) to build.
        using var scope = loadTestFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var load1 = await db.Users.SingleOrDefaultAsync(u => u.Email == "load1@zionshop.local");
        var load2 = await db.Users.SingleOrDefaultAsync(u => u.Email == "load2@zionshop.local");
        var load3 = await db.Users.SingleOrDefaultAsync(u => u.Email == "load3@zionshop.local");
        load1.Should().NotBeNull();
        load2.Should().NotBeNull();
        load3.Should().NotBeNull();
        load1!.EmailConfirmed.Should().BeTrue();
        load2!.EmailConfirmed.Should().BeTrue();
        load3!.EmailConfirmed.Should().BeTrue();

        var customer = await db.Users.SingleOrDefaultAsync(u => u.Email == "customer@zionshop.local");
        customer.Should().NotBeNull();
        customer!.EmailConfirmed.Should().BeTrue();

        // Re-running seeding against the same DB must not insert duplicates.
        await AuthDbSeeder.SeedAsync(db, loadTestCustomers: 3);

        var afterRerun = await db.Users.CountAsync(u => u.Email.StartsWith("load") && u.Email.EndsWith("@zionshop.local"));
        afterRerun.Should().Be(3);
    }

    [Fact]
    public async Task Login_Should_Succeed_When_UsingSeededCustomerCredentials()
    {
        var loadTestFactory = CreateLoadTestFactory(_factory, loadTestCustomers: 1);
        var client = loadTestFactory.CreateClient();

        var response = await client.LoginAsync("customer@zionshop.local", "Customer@123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
