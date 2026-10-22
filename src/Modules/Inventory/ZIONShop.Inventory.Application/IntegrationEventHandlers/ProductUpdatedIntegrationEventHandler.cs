using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Products;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Inventory.Application.Interfaces;
using ZIONShop.Inventory.Domain.Repositories;

namespace ZIONShop.Inventory.Application.IntegrationEventHandlers;

/// <summary>
/// Refreshes the denormalized product/variant snapshot (<see cref="Domain.Entities.InventoryItem.ProductName"/>,
/// <see cref="Domain.Entities.InventoryItem.Sku"/>, <see cref="Domain.Entities.InventoryItem.VariantLabel"/>)
/// carried on every inventory row for a product, after Products renames it or edits its variants.
/// The event itself is only a change SIGNAL (out-of-order redelivery can carry a stale payload) --
/// the write always comes from <see cref="IProductCatalogReader.ListVariantsByProductAsync"/>, the
/// authoritative current state, never from <c>@event.Name</c>/<c>@event.Variants</c>.
/// </summary>
public class ProductUpdatedIntegrationEventHandler : IIntegrationEventHandler<ProductUpdatedIntegrationEvent>
{
    private readonly IInventoryItemRepository _items;
    private readonly IInventoryUnitOfWork _uow;
    private readonly IProductCatalogReader _catalog;

    public ProductUpdatedIntegrationEventHandler(IInventoryItemRepository items, IInventoryUnitOfWork uow, IProductCatalogReader catalog)
    {
        _items = items;
        _uow = uow;
        _catalog = catalog;
    }

    public async Task HandleAsync(ProductUpdatedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var items = await _items.Query().Where(i => i.ProductId == @event.ProductId).ToListAsync(cancellationToken);
        if (items.Count == 0) return;

        var catalogVariants = await _catalog.ListVariantsByProductAsync(@event.ProductId, cancellationToken);
        if (catalogVariants.Count == 0) return; // product deleted in the catalog -- nothing authoritative to apply

        var catalogByVariantId = catalogVariants.ToDictionary(v => v.VariantId);
        var changed = false;

        foreach (var item in items)
        {
            if (!catalogByVariantId.TryGetValue(item.VariantId, out var variant)) continue; // unknown variant -- ignored

            if (item.RenameProduct(variant.ProductName)) changed = true;
            if (item.RenameVariant(variant.Sku, variant.VariantLabel)) changed = true;
        }

        if (!changed) return;

        await _uow.SaveChangesAsync(cancellationToken);
    }
}
