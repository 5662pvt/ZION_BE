using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Orders;

/// <summary>
/// Raised by Orders when an inventory event arrives for an order that is no longer <c>PendingStock</c> for a
/// reason other than a normal, already-processed transition (e.g. a delayed <c>OrderCreated</c> retry reserves
/// stock for an order the customer already cancelled) -- tells Inventory to release stock it reserved for an
/// order that will never consume it, instead of leaking the reservation forever.
/// </summary>
public sealed record OrderReservationReleaseRequestedIntegrationEvent(Guid OrderId, string Reason) : IntegrationEvent;
