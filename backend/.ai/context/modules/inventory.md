# Module — Inventory (Phase 2)

## Bounded context

Stock levels, reservation, release, concurrency.

## Key entities (suggested)

- `InventoryItem` (per `SkuId`), `StockReservation`

## Features (minimum Phase 2)

- `ReserveStock`, `ReleaseReservation`, `ConfirmReservation` (on payment)
- `AdjustStock` (admin)
- Background: expire reservations after **15 minutes**

## Invariants

Full rules: [11-inventory.md](../11-inventory.md)

## Events

- Consume: `OrderCreatedIntegrationEvent`
- Publish: `InventoryReservedIntegrationEvent`, `InventoryReservationFailedIntegrationEvent`

## DbContext

`InventoryDbContext` — schema `inventory`

## Docs to load

`07-database`, `11-inventory`, `10-event-driven`, `06-backend-rules`
