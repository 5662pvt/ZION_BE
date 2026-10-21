using ZIONShop.Contracts.Products;
using ZIONShop.Inventory.Domain.Exceptions;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Services;

/// <summary>
/// Shared resolution for the legacy <c>/inventory/products/{id}</c>-shaped routes, which must keep accepting
/// either a variant id or a product id: <paramref name="id"/> is tried as a variant id first (the common case,
/// since every pre-existing/default-variant product's single variant id equals its product id), but only taken
/// if it is ACTIVE or is a genuine non-default variant (VariantId != ProductId) -- a deactivated DEFAULT variant
/// (Id == ProductId, left behind when a legacy product is converted to options) is never ambiguous with "pick the
/// active variant for me", so it falls through to the product-id path below. That path looks at every variant of
/// the product: zero variants at all means the id is not a product either (<see cref="InventoryErrors.ProductNotFound"/>);
/// otherwise exactly one ACTIVE variant resolves to it, and zero or several ACTIVE variants resolve to
/// <see cref="InventoryErrors.VariantRequired"/> since the caller must pick a concrete variant.
/// </summary>
public static class ProductOrVariantIdResolver
{
    public static async Task<Result<Guid>> ResolveAsync(IProductCatalogReader catalog, Guid id, CancellationToken ct)
    {
        var variant = await catalog.GetVariantAsync(id, ct);
        if (variant is not null && (variant.IsActive || variant.VariantId != variant.ProductId))
            return Result.Success(variant.VariantId);

        var variants = await catalog.ListVariantsByProductAsync(id, ct);
        if (variants.Count == 0) return Result.Failure<Guid>(InventoryErrors.ProductNotFound);

        var activeVariants = variants.Where(v => v.IsActive).ToList();
        return activeVariants.Count switch
        {
            1 => Result.Success(activeVariants[0].VariantId),
            _ => Result.Failure<Guid>(InventoryErrors.VariantRequired)
        };
    }
}
