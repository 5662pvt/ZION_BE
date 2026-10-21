using ZIONShop.Contracts.Products;
using ZIONShop.Inventory.Domain.Exceptions;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Services;

/// <summary>
/// Shared resolution for the legacy <c>/inventory/products/{id}</c>-shaped routes, which must keep accepting
/// either a variant id or a product id: <paramref name="id"/> is tried as a variant id first (the common case,
/// since every pre-existing/default-variant product's single variant id equals its product id); if that
/// misses, it is tried as a product id -- a product with exactly one variant resolves to that variant, zero
/// resolves to <see cref="InventoryErrors.ProductNotFound"/>, and more than one resolves to
/// <see cref="InventoryErrors.VariantRequired"/> since the caller must pick a concrete variant.
/// </summary>
public static class ProductOrVariantIdResolver
{
    public static async Task<Result<Guid>> ResolveAsync(IProductCatalogReader catalog, Guid id, CancellationToken ct)
    {
        var variant = await catalog.GetVariantAsync(id, ct);
        if (variant is not null) return Result.Success(variant.VariantId);

        var variants = (await catalog.ListVariantsAsync(ct)).Where(v => v.ProductId == id).ToList();
        return variants.Count switch
        {
            0 => Result.Failure<Guid>(InventoryErrors.ProductNotFound),
            1 => Result.Success(variants[0].VariantId),
            _ => Result.Failure<Guid>(InventoryErrors.VariantRequired)
        };
    }
}
