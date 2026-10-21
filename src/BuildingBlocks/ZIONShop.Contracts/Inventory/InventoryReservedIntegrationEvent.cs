using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Inventory;

public sealed record InventoryReservedIntegrationEvent(Guid OrderId, IReadOnlyList<ReservedItem> Items, DateTime ExpiresAt) : IntegrationEvent;

/// <summary>VariantId ?? ProductId is the stock/price key.</summary>
public sealed record ReservedItem(Guid ProductId, int Quantity, Guid? VariantId = null);

public sealed record InventoryReservationFailedIntegrationEvent(Guid OrderId, string Reason, IReadOnlyList<Guid> UnavailableProductIds) : IntegrationEvent;

public sealed record InventoryReservationExpiredIntegrationEvent(Guid OrderId) : IntegrationEvent;
