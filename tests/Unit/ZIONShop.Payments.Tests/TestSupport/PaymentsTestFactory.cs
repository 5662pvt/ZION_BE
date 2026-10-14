using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZIONShop.Payments.Application.Options;
using ZIONShop.Payments.Infrastructure.Gateway;
using ZIONShop.Payments.Infrastructure.Persistence;
using ZIONShop.Payments.Infrastructure.Repositories;

namespace ZIONShop.Payments.Tests.TestSupport;

internal static class PaymentsTestFactory
{
    public const string WebhookSecret = "unit-test-fake-webhook-secret";
    public const string CheckoutBaseUrl = "http://localhost:3000";

    public static (PaymentsDbContext Db, PaymentRepository Payments, PayableOrderRepository PayableOrders, ProcessedWebhookEventRepository ProcessedEvents) BuildScope(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString("N"))
            .Options;
        var db = new PaymentsDbContext(options);
        return (db, new PaymentRepository(db), new PayableOrderRepository(db), new ProcessedWebhookEventRepository(db));
    }

    public static FakePaymentGateway BuildGateway(string webhookSecret = WebhookSecret, string checkoutBaseUrl = CheckoutBaseUrl) =>
        new(Options.Create(new FakeGatewayOptions { WebhookSecret = webhookSecret, CheckoutBaseUrl = checkoutBaseUrl }));
}
