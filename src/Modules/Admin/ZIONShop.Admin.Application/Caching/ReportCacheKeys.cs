using System.Globalization;

namespace ZIONShop.Admin.Application.Caching;

public static class ReportCacheKeys
{
    public static string Dashboard(DateOnly from, DateOnly to) => Build("dashboard", from, to);

    public static string TopProducts(DateOnly from, DateOnly to, int limit, string? sortBy) =>
        Build("topproducts", from, to, limit.ToString(CultureInfo.InvariantCulture), Normalize(sortBy));

    public static string TopCategories(DateOnly from, DateOnly to, string? sortBy) =>
        Build("topcategories", from, to, Normalize(sortBy));

    public static string PromotionPerformance(DateOnly from, DateOnly to) => Build("promotionperformance", from, to);

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? "revenue" : value.Trim().ToLowerInvariant();

    private static string Build(string query, DateOnly from, DateOnly to, params string[] extra)
    {
        var parts = new List<string>
        {
            "zion", "reports", query,
            from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
        parts.AddRange(extra);
        return string.Join(':', parts);
    }
}
