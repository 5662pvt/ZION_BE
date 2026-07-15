# 11 — Inventory Rules

**MUST enforce in Domain + Application.**

## Invariants

- Stock **cannot go negative**
- Orders **reserve** stock before payment
- Reservation **expires after 15 minutes** if not confirmed
- All mutations in **database transaction**
- Concurrency protection on every stock update

## Reservation flow

1. `ReserveStockCommand` — decrease available, increase reserved
2. `PaymentCompleted` — decrease reserved (sold)
3. `OrderCancelled` or timeout — release reserved to available

## Concurrency

Combine (see [07-database.md](07-database.md)):

1. SQL transaction
2. `RowVersion`
3. Redis lock (hot paths)
4. Queue reconciliation

**MUST** catch `DbUpdateConcurrencyException` → return **409 Conflict**.

## Integration events

- Consumes: `OrderCreatedIntegrationEvent` (reserve)
- Publishes: `InventoryReservedIntegrationEvent`, `InventoryReservationFailedIntegrationEvent`

## Related docs

- Orders: [modules/orders.md](modules/orders.md)
- Events: [10-event-driven.md](10-event-driven.md)
