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

        var rows = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                Price = p.Price.Amount,
                Currency = p.Price.Currency,
                p.Status,
                PrimaryImageUrl = p.Images
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => i.Url)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            r => r.Id,
            r => new ProductPriceInfo(r.Id, r.Name, r.Price, r.Currency, r.Status == ProductStatus.Published, r.Slug, r.PrimaryImageUrl));
    }
}
