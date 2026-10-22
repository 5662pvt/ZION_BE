using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Loyalty;

/// <summary>Source: "Order" | "Review" | "AdminAdjust".</summary>
public sealed record LoyaltyPointsEarnedIntegrationEvent(Guid UserId, int Points, string Source, Guid? OrderId, Guid? ProductId, DateTime ExpiresAt) : IntegrationEvent;

/// <summary>Used xu given back after cancel/refund.</summary>
public sealed record LoyaltyPointsReturnedIntegrationEvent(Guid UserId, int Points, Guid OrderId, string Reason) : IntegrationEvent;
