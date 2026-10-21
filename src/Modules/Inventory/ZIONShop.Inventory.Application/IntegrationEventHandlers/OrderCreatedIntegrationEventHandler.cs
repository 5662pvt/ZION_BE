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
/// Reserves stock for every line of a newly created order, all-or-nothing, inside one DB transaction.
/// Availability is enforced by <see cref="IInventoryItemRepository.TryReserveAsync"/>, a single atomic
/// conditional UPDATE per variant (OnHand - Reserved >= quantity) -- there is no read-then-write window,
/// so concurrent reservations for the same variant never conflict and never need a retry loop. Runs with
/// the scoped services the dispatcher resolved it with (same scope/DbContext as the inbox row the
/// IdempotentIntegrationEventHandler decorator added), so the inbox row commits atomically with the
/// reservation effect in the same transaction/SaveChanges call.
/// </summary>
public class OrderCreatedIntegrationEventHandler : IIntegrationEventHandler<OrderCreatedIntegrationEvent>
{
    private const string OutOfStockReason = "Hết hàng";

    private readonly IInventoryItemRepository _items;
    private readonly IStockReservationRepository _reservations;
    private readonly IStockMovementRepository _movements;
    private readonly IInventoryUnitOfWork _uow;
    private readonly IInventoryOutbox _outbox;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OrderCreatedIntegrationEventHandler> _logger;

    public OrderCreatedIntegrationEventHandler(
        IInventoryItemRepository items,
        IStockReservationRepository reservations,
        IStockMovementRepository movements,
        IInventoryUnitOfWork uow,
        IInventoryOutbox outbox,
        TimeProvider timeProvider,
        ILogger<OrderCreatedIntegrationEventHandler> logger)
    {
        _items = items;
        _reservations = reservations;
        _movements = movements;
        _uow = uow;
        _outbox = outbox;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreatedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _uow.BeginTransactionAsync(cancellationToken);

        var existing = await _reservations.GetByOrderIdAsync(@event.OrderId, cancellationToken);
        if (existing.Count > 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return; // already processed (idempotent)
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var isOnline = string.Equals(@event.PaymentMethod, "Online", StringComparison.OrdinalIgnoreCase);
        var expiresAt = isOnline ? now.AddMinutes(15) : (DateTime?)null;

        var unavailable = new List<Guid>();
        var toReserve = new List<(Guid ProductId, Guid VariantId, int Quantity)>();

        // Ordered by VariantId so two concurrent multi-line orders always acquire their row locks in
        // the same order, avoiding deadlocks between them.
        var groups = @event.Lines
            .GroupBy(l => l.VariantId ?? l.ProductId)
            .Select(g => new
            {
                VariantId = g.Key,
                ProductId = g.First().ProductId,
                Paid = g.Where(l => !l.IsGift).Sum(l => l.Quantity),
                Gift = g.Where(l => l.IsGift).Sum(l => l.Quantity)
            })
            .OrderBy(g => g.VariantId);

        foreach (var group in groups)
        {
            if (group.Paid > 0)
            {
                int reservedQuantity;
                if (group.Gift > 0 && await _items.TryReserveAsync(group.VariantId, group.Paid + group.Gift, cancellationToken))
                {
                    reservedQuantity = group.Paid + group.Gift;
                }
                else if (await _items.TryReserveAsync(group.VariantId, group.Paid, cancellationToken))
                {
                    reservedQuantity = group.Paid; // gift silently dropped, same rule as before
                }
                else
                {
                    unavailable.Add(group.VariantId);
                    continue;
                }

                toReserve.Add((group.ProductId, group.VariantId, reservedQuantity));
            }
            else if (group.Gift > 0 && await _items.TryReserveAsync(group.VariantId, group.Gift, cancellationToken))
            {
                toReserve.Add((group.ProductId, group.VariantId, group.Gift));
            }
            // else: gift-only line with no/insufficient inventory row -> nothing to reserve, gift dropped
        }

        if (unavailable.Count > 0)
        {
            // Undo every TryReserveAsync applied earlier in this loop -- they are all part of this one
            // DB transaction, so rolling it back reverts them atomically.
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogInformation(
                "Order {OrderId} could not reserve stock for variants {VariantIds}; cancelling with reason '{Reason}'",
                @event.OrderId, unavailable, OutOfStockReason);

            _outbox.Add(new InventoryReservationFailedIntegrationEvent(@event.OrderId, OutOfStockReason, unavailable));
            await _uow.SaveChangesAsync(cancellationToken); // fresh, non-transactional save for the failure outbox entry
            return;
        }

        foreach (var (productId, variantId, quantity) in toReserve)
        {
            await _reservations.AddAsync(StockReservation.Create(@event.OrderId, productId, variantId, quantity, expiresAt, now), cancellationToken);
            await _movements.AddAsync(StockMovement.Create(productId, variantId, StockMovementType.Reserve, quantity, @event.OrderId.ToString(), now), cancellationToken);
        }

        _outbox.Add(new InventoryReservedIntegrationEvent(
            @event.OrderId,
            toReserve.Select(x => new ReservedItem(x.ProductId, x.Quantity, x.VariantId)).ToList(),
            expiresAt ?? DateTime.MaxValue));

        await _uow.SaveChangesAsync(cancellationToken); // also persists the inbox row added by the decorator
        await transaction.CommitAsync(cancellationToken);
    }
}
