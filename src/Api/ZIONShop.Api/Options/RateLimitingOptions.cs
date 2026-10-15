namespace ZIONShop.Api.Options;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthPermitLimit { get; set; } = 20;
    public int CheckoutPermitLimit { get; set; } = 10;
    public int SearchPermitLimit { get; set; } = 60;
    public int WindowSeconds { get; set; } = 60;
}
