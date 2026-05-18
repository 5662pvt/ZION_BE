using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Domain.Enums;
using ZIONShop.Products.Infrastructure.Persistence;

namespace ZIONShop.Products.Infrastructure.Services;

public class ProductPriceLookup : IProductPriceLookup
{
    private readonly ProductsDbContext _db;

    public ProductPriceLookup(ProductsDbContext db) => _db = db;

    public async Task<ProductPriceInfo?> GetAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var row = await _db.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new { p.Id, p.Name, Price = p.Price.Amount, Currency = p.Price.Currency, p.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        return new ProductPriceInfo(row.Id, row.Name, row.Price, row.Currency, row.Status == ProductStatus.Published);
    }
}
