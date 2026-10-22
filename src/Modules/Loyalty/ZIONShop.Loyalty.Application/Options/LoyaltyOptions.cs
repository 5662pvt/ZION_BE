namespace ZIONShop.Loyalty.Application.Options;

public class LoyaltyOptions
{
    public const string SectionName = "Loyalty";

    public int EarnRatePercent { get; set; } = 1;
    public int ReviewBonus { get; set; } = 100;
    public int MaxRedeemPercent { get; set; } = 50;
    public int LotLifetimeMonths { get; set; } = 12;
    public int RestoreLotDays { get; set; } = 30;
    public int ExpiryScanMinutes { get; set; } = 60;
}
