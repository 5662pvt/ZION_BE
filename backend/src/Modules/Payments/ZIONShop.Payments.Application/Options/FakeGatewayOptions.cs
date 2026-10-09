namespace ZIONShop.Payments.Application.Options;

public class FakeGatewayOptions
{
    public const string SectionName = "Payments:Fake";

    public string WebhookSecret { get; set; } = string.Empty;
    public string CheckoutBaseUrl { get; set; } = "http://localhost:3000";
}
