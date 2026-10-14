using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Inventory.Application.Interfaces;

/// <summary>
/// Thin, DbContext-agnostic façade over <c>IOutbox&lt;InventoryDbContext&gt;</c> so Application code
/// never has to reference the concrete EF Core type that lives in Infrastructure.
/// </summary>
public interface IInventoryOutbox
{
    void Add(IntegrationEvent @event);
}
