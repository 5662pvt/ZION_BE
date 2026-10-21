using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Products;
using ZIONShop.Products.Domain.Enums;
using ZIONShop.Products.Infrastructure.Persistence;

namespace ZIONShop.Products.Infrastructure.Services;

public class ProductPriceLookup : IProductPriceLookup
{
    private readonly ProductsDbContext _db;

    public ProductPriceLookup(ProductsDbContext db) => _db = db;

    public async Task<ProductPriceInfo?> GetAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var infos = await GetManyAsync(new[] { productId }, cancellationToken);
        return infos.TryGetValue(productId, out var info) ? info : null;
    }

    public async Task<IReadOnlyDictionary<Guid, ProductPriceInfo>> GetManyAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0) return new Dictionary<Guid, ProductPriceInfo>();

        var result = new Dictionary<Guid, ProductPriceInfo>();

        var variantRows = await _db.ProductVariants.AsNoTracking()
            .Where(v => productIds.Contains(v.Id))
            .Join(_db.Products.AsNoTracking(), v => v.ProductId, p => p.Id, (v, p) => new
            {
                v.Id,
                v.ProductId,
                p.Name,
                Price = v.Price.Amount,
                Currency = v.Price.Currency,
                Available = p.Status == ProductStatus.Published && v.IsActive,
                p.Slug,
                PrimaryImageUrl = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                v.Option1Value,
                v.Option2Value,
                Sku = v.Sku.Value
            })
            .ToListAsync(cancellationToken);

        foreach (var row in variantRows)
        {
            var label = string.Join(" / ", new[] { row.Option1Value, row.Option2Value }.Where(v => !string.IsNullOrWhiteSpace(v)));
            result[row.Id] = new ProductPriceInfo(row.ProductId, row.Name, row.Price, row.Currency, row.Available, row.Slug, row.PrimaryImageUrl, row.Id, label, row.Sku);
        }

        var remainingIds = productIds.Where(id => !result.ContainsKey(id)).ToList();
        if (remainingIds.Count == 0) return result;

        var productRows = await _db.Products.AsNoTracking()
            .Where(p => remainingIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.Status,
                PrimaryImageUrl = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                ActiveVariants = p.Variants.Where(v => v.IsActive)
                    .Select(v => new { v.Id, Price = v.Price.Amount, Currency = v.Price.Currency, v.Option1Value, v.Option2Value, Sku = v.Sku.Value })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        foreach (var row in productRows)
        {
            if (row.ActiveVariants.Count == 1)
            {
                var v = row.ActiveVariants[0];
                var label = string.Join(" / ", new[] { v.Option1Value, v.Option2Value }.Where(x => !string.IsNullOrWhiteSpace(x)));
                var available = row.Status == ProductStatus.Published;
                result[row.Id] = new ProductPriceInfo(row.Id, row.Name, v.Price, v.Currency, available, row.Slug, row.PrimaryImageUrl, v.Id, label, v.Sku);
            }
            else
            {
                var fallback = row.ActiveVariants.FirstOrDefault();
                result[row.Id] = new ProductPriceInfo(row.Id, row.Name, fallback?.Price ?? 0m, fallback?.Currency ?? "VND", false, row.Slug, row.PrimaryImageUrl);
            }
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, ProductSummaryInfo>> GetProductSummaryAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0) return new Dictionary<Guid, ProductSummaryInfo>();

        var rows = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.MinPrice,
                p.Status,
                PrimaryImageUrl = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                Currency = p.Variants.Where(v => v.IsActive).OrderBy(v => v.Price.Amount).Select(v => v.Price.Currency).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            r => r.Id,
            r => new ProductSummaryInfo(r.Id, r.Name, r.Slug, r.MinPrice, r.Currency ?? "VND", r.PrimaryImageUrl, r.Status == ProductStatus.Published));
    }
}
