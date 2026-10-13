using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Products;
using ZIONShop.Products.Infrastructure.Persistence;

namespace ZIONShop.Products.Infrastructure.Readers;

public class CategoryLookup : ICategoryLookup
{
    private readonly ProductsDbContext _db;

    public CategoryLookup(ProductsDbContext db) => _db = db;

    public async Task<IReadOnlyList<CategoryRef>> ListAsync(CancellationToken ct = default) =>
        await _db.Categories.AsNoTracking()
            .Select(c => new CategoryRef(c.Id, c.Name))
            .ToListAsync(ct);
}
