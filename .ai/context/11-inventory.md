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

### Reservation path: atomic conditional UPDATE, not xmin + retry

`OrderCreatedIntegrationEventHandler` reserves stock with `IInventoryItemRepository.TryReserveAsync`,
a single conditional `UPDATE "InventoryItems" SET "Reserved" = "Reserved" + @q WHERE "ProductId" = @p
AND "OnHand" - "Reserved" >= @q` (EF Core `ExecuteUpdateAsync`), returning whether a row matched. There
is no read-then-write window, so two concurrent reservations for the same product never race and never
need a retry loop — the previous design (`item.Reserve()` + xmin-token `SaveChangesAsync`, retried up to
3× in a fresh `IServiceScopeFactory` scope on conflict) could both falsely cancel an order ("Hết hàng")
while stock was still available *and* bypass the inbox row added to the handler's own scope (the fresh
retry scopes used a different `DbContext` instance than the one the `IdempotentIntegrationEventHandler`
decorator recorded the inbox row on).

Because `ExecuteUpdateAsync` bypasses the EF change tracker, the handler never loads/keeps a tracked
`InventoryItem` for a product it reserves in the same scope — doing so risks later saving that stale
tracked instance over the row `TryReserveAsync` just changed directly in the database.

**Transaction boundary.** The handler opens one explicit transaction via
`IInventoryUnitOfWork.BeginTransactionAsync()` (wraps `IDbContextTransaction` behind
`IInventoryTransaction`, so Application code never references the EF type) for the whole order: every
`TryReserveAsync` call enlists in it automatically, as does the final `SaveChangesAsync` that persists
the new `StockReservation`/`StockMovement` rows, the outbox entry, and — critically — the inbox row the
`IdempotentIntegrationEventHandler` decorator added on the *same* scoped `DbContext` before calling this
handler. One commit makes the reservation effect and the inbox row durable together; one failed product
rolls the whole transaction back (undoing every `TryReserveAsync` applied so far for that order), after
which a fresh, non-transactional `SaveChangesAsync` persists just the failure outbox entry.

**Lock ordering.** Multi-product orders process their product groups sorted by `ProductId`, so two
concurrent orders that share two products always acquire row locks in the same order — avoiding a
Postgres deadlock (`40P01`) between them.

**Why not a Redis lock for this path**: an external lock only protects callers that remember to take it;
a DB-level conditional UPDATE is enforced by Postgres itself for every writer, including anything that
bypasses the lock by accident, and needs no extra infrastructure/TTL-expiry bookkeeping. Redis locks stay
useful for coordinating non-transactional, cross-service work — not for a single conditional row update
that Postgres can already make atomic.

### Release/commit path: atomic conditional UPDATE too (W16 cleanup)

Through W11, release/commit/return (`OrderCancelledIntegrationEventHandler`, `ActiveReservationReleaser`,
`OrderConfirmedIntegrationEventHandler`, `OrderReservationReleaseRequestedIntegrationEventHandler`,
`ReservationExpiryProcessor`) still loaded a tracked `InventoryItem`, mutated it with the entity method
(`Release`/`Commit`/`Return`), and let EF batch the actual `UPDATE` at `SaveChangesAsync` time — in EF's
own (unordered) batching order, not necessarily the order the caller looped in. Two concurrent
multi-product operations that touched the same products in opposite order (e.g. a cancel releasing A,B
while another order reserved B,A, or the expiry job releasing many products while several orders committed)
could deadlock (Postgres `40P01`) or throw `DbUpdateConcurrencyException` on the xmin token.

Fixed the same way as `TryReserveAsync`: `IInventoryItemRepository.TryReleaseAsync` (`Reserved -= q WHERE
Reserved >= q`), `TryCommitAsync` (`OnHand -= q, Reserved -= q WHERE Reserved >= q AND OnHand >= q`), and
`TryReturnAsync` (`OnHand += q`) are immediate, single-row `ExecuteUpdateAsync` conditional UPDATEs — no
tracked row, no deferred/reordered write. Every call site now processes its reservations **ordered by
ProductId** before issuing them:

- `OrderConfirmedIntegrationEventHandler` — active reservations, ordered, committed via `TryCommitAsync`.
- `ActiveReservationReleaser` (used by `OrderReservationReleaseRequestedIntegrationEventHandler`) — active
  reservations, ordered, released via `TryReleaseAsync`.
- `ReservationExpiryProcessor` — the **whole** expired batch (not grouped/ordered per order) sorted by
  ProductId, released via `TryReleaseAsync`, inside its own explicit transaction (`BeginTransactionAsync`
  — unlike the integration event handlers below, nothing already opened one on this scope, since the
  background service calls it directly rather than through the inbox-idempotent dispatch).
- `OrderCancelledIntegrationEventHandler` — its order's Active *and* Committed reservations are merged into
  **one** pass ordered by ProductId (release via `TryReleaseAsync`, return via `TryReturnAsync`), not two
  separate ordered-but-sequential phases. Splitting by status first would still risk inversion: one order's
  Active lines and another concurrent order's Committed lines for the same two products could land in
  opposite phase order across the two phases even though each phase alone was sorted.

Admin `Adjust` (`AdjustStockCommandHandler`) is unchanged — single-row, tracked entity + xmin, catching
`DbUpdateConcurrencyException` into a 409 for the caller to retry. A single-row update can't invert lock
order against itself, so it doesn't need the atomic-UPDATE treatment; it already returns a conflict to the
admin UI instead of relying on bus redelivery, which is the right behavior for a human-triggered action.

If a path still hits a transient DB error (serialization failure, deadlock despite the ordering), it must
propagate, not be swallowed — the integration event handlers have no local catch around these calls, so an
exception rolls back the inbox decorator's transaction and the message is redelivered.

## Integration events

- Consumes: `OrderCreatedIntegrationEvent` (reserve), `OrderConfirmedIntegrationEvent` (commit), `OrderCancelledIntegrationEvent` (release active / return committed), `OrderReservationReleaseRequestedIntegrationEvent` (release active — W11 rework, see below)
- Publishes: `InventoryReservedIntegrationEvent`, `InventoryReservationFailedIntegrationEvent`

### Releasing a reservation that outlived its order (W11 rework)

A reservation can land for an order that is already `Cancelled` (e.g. `OrderCreated` was stuck in a delayed
retry queue while the customer cancelled the still-`PendingStock` order; `OrderCancelledIntegrationEventHandler`
found nothing to release at that time because the reservation didn't exist yet). Orders detects this in
`InventoryReservedIntegrationEventHandler` and publishes `OrderReservationReleaseRequestedIntegrationEvent`
instead of silently no-op'ing. `OrderReservationReleaseRequestedIntegrationEventHandler` releases that order's
*active* reservations the same way `OrderCancelledIntegrationEventHandler` does (shared
`ActiveReservationReleaser.ReleaseAsync` helper) — only the active half, since a reservation released this way
was never committed (the order never reached `Confirmed`).

## Related docs

- Orders: [modules/orders.md](modules/orders.md)
- Events: [10-event-driven.md](10-event-driven.md)
