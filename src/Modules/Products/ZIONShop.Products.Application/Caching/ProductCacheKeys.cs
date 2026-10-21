namespace ZIONShop.Products.Application.Caching;

public static class ProductCacheKeys
{
    public const string CategoryTree = "zion:categories:tree";

    public static string DetailById(Guid id) => $"zion:products:v2:detail:id:{id}";

    public static string DetailBySlug(string slug) => $"zion:products:v2:detail:slug:{slug.Trim().ToLowerInvariant()}";
}
