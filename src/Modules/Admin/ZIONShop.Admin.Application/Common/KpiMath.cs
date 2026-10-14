namespace ZIONShop.Admin.Application.Common;

internal static class KpiMath
{
    public static decimal? ChangePercent(decimal current, decimal previous)
    {
        if (previous == 0) return null;
        return Math.Round((current - previous) / previous * 100m, 1, MidpointRounding.AwayFromZero);
    }
}
