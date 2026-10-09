using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Inventory;

public sealed record InventoryReservedIntegrationEvent(Guid OrderId, IReadOnlyList<ReservedItem> Items, DateTime ExpiresAt) : IntegrationEvent;

public sealed record ReservedItem(Guid ProductId, int Quantity);

public sealed record InventoryReservationFailedIntegrationEvent(Guid OrderId, string Reason, IReadOnlyList<Guid> UnavailableProductIds) : IntegrationEvent;

public sealed record InventoryReservationExpiredIntegrationEvent(Guid OrderId) : IntegrationEvent;
