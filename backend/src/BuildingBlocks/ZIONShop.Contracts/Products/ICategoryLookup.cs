namespace ZIONShop.Contracts.Products;

public sealed record CategoryRef(Guid CategoryId, string Name);

public interface ICategoryLookup
{
    /// <summary>All non-deleted categories.</summary>
    Task<IReadOnlyList<CategoryRef>> ListAsync(CancellationToken ct = default);
}
