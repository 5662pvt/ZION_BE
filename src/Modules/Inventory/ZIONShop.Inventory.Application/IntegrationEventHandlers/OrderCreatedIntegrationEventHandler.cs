using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Inventory.Application.Interfaces;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Domain.Repositories;

namespace ZIONShop.Inventory.Application.IntegrationEventHandlers;

/// <summary>
/// Reserves stock for every line of a newly created order, all-or-nothing. Retries the whole
/// attempt (with a fresh scope/DbContext so the xmin concurrency token is reloaded) when a
/// concurrent reservation on the same product causes a save conflict.
/// </summary>
public class OrderCreatedIntegrationEventHandler : IIntegrationEventHandler<OrderCreatedIntegrationEvent>
{
    private const int MaxAttempts = 3;
    private const string OutOfStockReason = "Hết hàng";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OrderCreatedIntegrationEventHandler> _logger;

    public OrderCreatedIntegrationEventHandler(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<OrderCreatedIntegrationEventHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreatedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var reservations = sp.GetRequiredService<IStockReservationRepository>();
            var items = sp.GetRequiredService<IInventoryItemRepository>();
            var movements = sp.GetRequiredService<IStockMovementRepository>();
            var uow = sp.GetRequiredService<IInventoryUnitOfWork>();
            var outbox = sp.GetRequiredService<IInventoryOutbox>();

            var existing = await reservations.GetByOrderIdAsync(@event.OrderId, cancellationToken);
            if (existing.Count > 0) return; // already processed (idempotent)

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var isOnline = string.Equals(@event.PaymentMethod, "Online", StringComparison.OrdinalIgnoreCase);
            var expiresAt = isOnline ? now.AddMinutes(15) : (DateTime?)null;

            var unavailable = new List<Guid>();
            var toReserve = new List<(InventoryItem Item, int Quantity)>();

            var groups = @event.Lines
                .GroupBy(l => l.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Paid = g.Where(l => !l.IsGift).Sum(l => l.Quantity),
                    Gift = g.Where(l => l.IsGift).Sum(l => l.Quantity)
                });

            foreach (var group in groups)
            {
                var item = await items.GetByProductIdAsync(group.ProductId, cancellationToken);
                var available = item?.Available ?? 0;

                if (group.Paid > 0 && available < group.Paid)
                {
                    unavailable.Add(group.ProductId);
                    continue;
                }

                if (item is null) continue; // gift-only product with no inventory record -> nothing to reserve, gift dropped

                var quantity = group.Paid + (available >= group.Paid + group.Gift ? group.Gift : 0);
                if (quantity > 0)
                    toReserve.Add((item, quantity));
            }

            if (unavailable.Count > 0)
            {
                outbox.Add(new InventoryReservationFailedIntegrationEvent(@event.OrderId, OutOfStockReason, unavailable));
                await uow.SaveChangesAsync(cancellationToken);
                return;
            }

            foreach (var (item, quantity) in toReserve)
            {
                item.Reserve(quantity);
                await reservations.AddAsync(StockReservation.Create(@event.OrderId, item.ProductId, quantity, expiresAt, now), cancellationToken);
                await movements.AddAsync(StockMovement.Create(item.ProductId, StockMovementType.Reserve, quantity, @event.OrderId.ToString(), now), cancellationToken);
            }

            outbox.Add(new InventoryReservedIntegrationEvent(
                @event.OrderId,
                toReserve.Select(x => new ReservedItem(x.Item.ProductId, x.Quantity)).ToList(),
                expiresAt ?? DateTime.MaxValue));

            try
            {
                await uow.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict reserving stock for order {OrderId}, attempt {Attempt}/{MaxAttempts}", @event.OrderId, attempt, MaxAttempts);
            }
        }

        // All retries exhausted: fail the reservation instead of leaving the order stuck.
        using var finalScope = _scopeFactory.CreateScope();
        var finalOutbox = finalScope.ServiceProvider.GetRequiredService<IInventoryOutbox>();
        var finalUow = finalScope.ServiceProvider.GetRequiredService<IInventoryUnitOfWork>();
        finalOutbox.Add(new InventoryReservationFailedIntegrationEvent(@event.OrderId, OutOfStockReason, @event.Lines.Select(l => l.ProductId).ToList()));
        await finalUow.SaveChangesAsync(cancellationToken);
    }
}
