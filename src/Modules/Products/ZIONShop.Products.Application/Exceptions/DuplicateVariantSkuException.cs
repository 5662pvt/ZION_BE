namespace ZIONShop.Products.Application.Exceptions;

/// <summary>
/// Thrown by <see cref="Interfaces.IProductsUnitOfWork.SaveChangesAsync"/> when the save violates the unique
/// index on ProductVariants.Sku — a defensive backstop for a SKU collision that slipped past the handler's
/// own checks (e.g. a race between two concurrent SetVariants calls). Infrastructure translates the
/// provider-specific unique-violation into this Application-level type so handlers never reference Npgsql.
/// </summary>
public sealed class DuplicateVariantSkuException : Exception
{
    public DuplicateVariantSkuException(Exception innerException)
        : base("Variant SKU already exists.", innerException)
    {
    }
}
