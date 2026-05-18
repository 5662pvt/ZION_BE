using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Inventory;

public sealed record InventoryReservedIntegrationEvent(Guid OrderId, IReadOnlyList<ReservedItem> Items, DateTime ExpiresAt) : IntegrationEvent;

public sealed record ReservedItem(Guid ProductId, int Quantity);
