# Module — Inventory (Phase 2)

## Bounded context

Stock levels, reservation, release, concurrency.

## Key entities (implemented W4)

- `InventoryItem` (per `ProductId`, unique): `OnHand`, `Reserved`, `Available = OnHand - Reserved`, `LowStockThreshold` (default 5).
  Methods: `Reserve`, `Release`, `Commit`, `Adjust` — all return `Result`, never throw, never let `OnHand < Reserved` or go negative.
  Concurrency: PostgreSQL `xmin` system column (`builder.UseXminAsConcurrencyToken()` — obsolete-but-functional in this Npgsql version; flagged as a follow-up to migrate to `IsRowVersion()`/`[Timestamp]`).
- `StockReservation`: `OrderId`, `ProductId`, `Quantity`, `Status` (`Active|Committed|Released|Expired`), `ExpiresAt?`, `CreatedAt`. Unique `(OrderId, ProductId)`.
- `StockMovement`: audit log — `ProductId`, `Type` (`Reserve|Release|Commit|Adjust|Expire`), `Quantity`, `Reference` (OrderId or admin note), `CreatedAt`, `CreatedBy?`.

## Features (implemented W4)

- `GetAvailability(productId)` `[AllowAnonymous]` → `{ productId, available, inStock }` (missing item → `available: 0`).
- `SearchStock(page,pageSize,keyword?,lowStockOnly)` `[StaffOrAdmin]` — paged.
- `GetStockMovements(productId,page,pageSize)` `[StaffOrAdmin]` — paged.
- `AdjustStock(productId, delta, reason)` `[AdminOnly]` — creates the `InventoryItem` from `IProductCatalogReader` if missing (OnHand starts at 0, then adjusted); concurrency conflict → `Inventory.ConcurrencyConflict` (409).
- `ReservationExpiryProcessor` (business logic) + `ReservationExpiryBackgroundService` (hosted service, Infrastructure) — scans every `Inventory:ExpiryScanSeconds` (config key, default 60; not added to appsettings so it falls back to the default) for `Active` reservations past `ExpiresAt`, releases them, marks `Expired`, logs a `StockMovement`, and publishes one `InventoryReservationExpiredIntegrationEvent` per affected order.

## Invariants

Full rules: [11-inventory.md](../11-inventory.md)

## Events

- Consume: `OrderCreatedIntegrationEvent`, `OrderConfirmedIntegrationEvent`, `OrderCancelledIntegrationEvent`.
  `OrderCreated` handler is idempotent (checks existing reservations by `OrderId` first) and all-or-nothing: every line's availability is checked before any `Reserve()` call, so either every line gets reserved or none do. ExpiresAt = now + 15 min when `PaymentMethod == "Online"`, else `null` (COD is confirmed right away); the outbox event's `ExpiresAt` uses `DateTime.MaxValue` as the "no expiry" sentinel since the contract field is non-nullable. On `DbUpdateConcurrencyException` the whole attempt (including the all-or-nothing availability check) retries up to 3 times, each time with a fresh `IServiceScope` so item rows and their `xmin` token are reloaded; if all retries fail, the reservation fails with reason `"Hết hàng"`.
- Publish: `InventoryReservedIntegrationEvent`, `InventoryReservationFailedIntegrationEvent`, `InventoryReservationExpiredIntegrationEvent` — all via the transactional outbox (`AddOutbox<InventoryDbContext>()`).
- Application code never references the concrete `InventoryDbContext` (that would be an Application → Infrastructure layering violation and a circular project reference, since `IOutbox<TDbContext>` is generic over the EF type). Instead Application defines a module-local `IInventoryOutbox` façade; Infrastructure's `InventoryOutbox` wraps `IOutbox<InventoryDbContext>` to implement it.

## Quyết định checkout MVP (2026-10-08)

Stock khóa theo `ProductId` (chưa có variant). Chi tiết luồng checkout: [modules/orders.md](orders.md).

## REST contract (W4)

- `GET /api/v1/inventory/products/{productId}` `[AllowAnonymous]` → `{ productId, available, inStock }`
- `GET /api/v1/inventory?page&pageSize&keyword&lowStockOnly` `[StaffOrAdmin]` → paged `InventoryItemDto { productId, sku, productName, onHand, reserved, available, lowStockThreshold, isLowStock }`
- `GET /api/v1/inventory/products/{productId}/movements?page&pageSize` `[StaffOrAdmin]` → paged `StockMovementDto { id, productId, type, quantity, reference, createdAt, createdBy }`
- `POST /api/v1/inventory/products/{productId}/adjust` `[AdminOnly]` — body `{ delta, reason }` → `InventoryItemDto`; 409 on concurrent adjust conflict.

## DbContext

`InventoryDbContext` — schema `inventory`, migration `InitialInventory` (tables `InventoryItems`, `StockReservations`, `StockMovements`, `outbox_messages`).
Dev seeder `InventoryDbSeeder` — for every product from `IProductCatalogReader` without an `InventoryItem`, creates one with `OnHand = 100`; runs after the Products seed in `WebApplicationExtensions.MigrateAndSeedAsync`.

## Docs to load

`07-database`, `11-inventory`, `10-event-driven`, `06-backend-rules`
