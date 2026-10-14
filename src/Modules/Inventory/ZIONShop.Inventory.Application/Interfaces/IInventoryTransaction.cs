namespace ZIONShop.Inventory.Application.Interfaces;

/// <summary>
/// Thin, EF-agnostic façade over <c>IDbContextTransaction</c> so Application code never has to
/// reference the concrete EF Core type that lives in Infrastructure.
/// </summary>
public interface IInventoryTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
