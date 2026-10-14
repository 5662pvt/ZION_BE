# Module — Orders (Phase 2)

## Bounded context

Order lifecycle: draft → placed → paid → confirmed → shipped → completed / cancelled.

## Key entities (suggested)

- `Order`, `OrderLine`, `OrderStatus` enum, shipping address snapshot

## Features (minimum Phase 2)

- `CreateOrderFromCart` / `PlaceOrder`
- `GetOrder`, `GetOrdersForUser`, `CancelOrder`

## Events (MUST)

Publish after commit:

- `OrderCreatedIntegrationEvent` → Inventory reserves stock
- Later: `OrderConfirmedIntegrationEvent`, `OrderCancelledIntegrationEvent`

## Dependencies

- Cart (read cart)
- Inventory (via events only)
- Payments (via events)

## FE module (ZION_FE)

`app/(shop)/orders/`, `app/(shop)/checkout/`, `services/orders.service.ts`, `hooks/useOrders.ts`

## Quyết định checkout MVP (2026-10-08)

- Yêu cầu đăng nhập (login required) để checkout.
- Phương thức thanh toán: `COD` | `Online` (Online đi qua `IPaymentGateway`, MVP dùng `FakePaymentGateway`).
- Phí ship cấu hình qua `Checkout:ShippingFee` = 30000 VND, `Checkout:FreeShippingThreshold` = 500000 VND (subtotal >= ngưỡng → phí ship = 0).
- Address snapshot lấy từ address book của user; giá snapshot vào `OrderLine` tại thời điểm đặt hàng (không tham chiếu giá sống của Products).
- Luồng event bất đồng bộ:
  `PendingStock` → (`InventoryReserved`) → `AwaitingPayment` [Online] / `Confirmed` [COD] → `Paid` → `Shipped` → `Completed`.
  `Cancelled` khi: reservation thất bại, payment thất bại, khách hủy trước khi ship, hoặc reservation hết hạn sau 15 phút.
- Integration event publish qua transactional outbox (BuildingBlocks) — sẽ xây ở W3.

## REST contract (W4/W5)

- `POST /api/v1/orders` `[Authorize]` — body `{ addressId, paymentMethod: "COD"|"Online", note? }` → `OrderDto`:
  `{ id, code, status, paymentMethod, subtotal, shippingFee, total, currency, createdAt, shippingAddress: AddressSnapshot, lines: [{ productId, productName, unitPrice, quantity, lineTotal }], history: [{ status, at, note }] }`
  (`history` added W6 — ordered by `at` ascending, sourced from `Order.StatusHistory`, already eager-loaded by `IOrderRepository.GetByIdAsync`.)
- `GET /api/v1/orders/{id}` `[Authorize]` → `OrderDto` (owner or `StaffOrAdmin`, else 404)
- `GET /api/v1/orders/mine?page&pageSize&status` `[Authorize]` → paged `OrderSummaryDto { id, code, status, total, currency, createdAt, itemCount }`
- `POST /api/v1/orders/{id}/cancel` `[Authorize]` — body `{ reason? }`. Owner: allowed up to `Confirmed` (`PendingStock|AwaitingPayment|Confirmed`). `StaffOrAdmin`: allowed on **any** order up to `Paid` (adds `Paid`), i.e. anything before `Shipped`.
- Admin `[StaffOrAdmin]`:
  - `GET /api/v1/orders?page&pageSize&status&from&to&keyword` → paged `AdminOrderSummaryDto { id, code, status, paymentMethod, total, currency, createdAt, itemCount, recipientName, phoneNumber }` (W6 — `OrderSummaryDto` was used here through W5; `GET /orders/mine` is unaffected and still returns `OrderSummaryDto`). `keyword` matches `Code` OR `ShippingAddress.RecipientName` OR `ShippingAddress.PhoneNumber` (case-insensitive contains, same `UPPER` + `LIKE` + escape approach as before). `from`/`to` are inclusive day-range filters on `CreatedDate` (UTC): `from.Date <= CreatedDate < to.Date + 1 day`. `SearchOrdersQueryValidator` rejects `from > to`.
  - `POST /api/v1/orders/{id}/ship` (`Confirmed|Paid` → `Shipped`)
  - `POST /api/v1/orders/{id}/complete` (`Shipped` → `Completed`)

**W4 deviation from the original sketch above:** there is no manual `confirm` admin action — `Confirmed`/`AwaitingPayment` are set automatically by the `InventoryReserved` event handler (COD → `Confirmed`, Online → `AwaitingPayment`), and `Paid` is set automatically by `PaymentCompleted`. Admin only acts at `ship` and `complete`, plus `cancel` (shared with the owner route, gated by role inside the handler rather than a separate endpoint).

Status values: `PendingStock`, `AwaitingPayment`, `Confirmed`, `Paid`, `Shipped`, `Completed`, `Cancelled`.

## Order code format

`ZS` + `yyMMdd` + 4 random uppercase-alphanumeric chars, no separators — e.g. `ZS261009A1B2`. Enforced unique via a DB index, not uniqueness-checked at generation time (collision probability is negligible at this volume).

## Integration events (W4, implemented)

Handled by `ZIONShop.Orders.Application.IntegrationEvents.*` (registered in `OrdersInfrastructureExtensions`), all idempotent via domain state-guard (a handler's domain transition fails silently/no-ops if the order is no longer in the expected source state — not via `EventId` dedup):

- `InventoryReserved` → `PendingStock` → `Confirmed` (COD, publishes `OrderConfirmed`) or `AwaitingPayment` (Online).
- `InventoryReservationFailed` → `PendingStock` → `Cancelled` ("Hết hàng") + publishes `OrderCancelled` (W11). Inventory/Payments/Promotions also react to `InventoryReservationFailed` directly (nothing was reserved/paid for them to undo), and their `OrderCancelled` handlers are all guarded to no-op when there is nothing left to release, so Notifications seeing both events for the same order is safe — it only emails once, gated on `(EventId, NotificationType)`.
- `InventoryReservationExpired` → `AwaitingPayment` → `Cancelled` ("Quá hạn thanh toán") + publishes `OrderCancelled`.
- `PaymentCompleted` → `AwaitingPayment` → `Paid` + publishes `OrderConfirmed`.
- `PaymentFailed` → `AwaitingPayment` → `Cancelled` ("Thanh toán thất bại") + publishes `OrderCancelled`.

## Idempotency-Key on place order (W11)

`POST /api/v1/orders` accepts an optional `Idempotency-Key` header (1..100 chars, `[A-Za-z0-9_\-:.]`; format enforced in `PlaceOrderCommandHandler`, not the FluentValidation validator — `ValidationBehavior` always prefixes a validator failure code with the property name, so it cannot produce the exact `Orders.InvalidIdempotencyKey` contract code; same reasoning already applied to `PaymentMethod`'s shape check).

- Scope: `(UserId, Key)`, stored in `orders.idempotency_keys` (`OrderIdempotencyKey` entity/`IOrderIdempotencyKeyRepository`), unique index `IX_idempotency_keys_UserId_Key`.
- Request fingerprint: SHA-256 of `addressId (lower) | paymentMethod (lower) | note (trim, null→"") | couponCode (trim, upper, null→"")` (`IdempotencyFingerprint.Compute`).
- First request: order + key row saved in the same `SaveChangesAsync`. Same key + same fingerprint again (sequential or concurrent): no new order, 200 with the existing order + response header `Idempotent-Replayed: true`. Same key + different fingerprint: 409 `Orders.IdempotencyKeyReused`.
- Concurrency: before any side effect, the handler acquires a PostgreSQL session advisory lock for `(UserId, Key)` via `IIdempotencyLock` (Application interface, `PostgresIdempotencyLock` in Infrastructure — opens `OrdersDbContext`'s own connection explicitly, polls `pg_try_advisory_lock` every ~100ms up to ~10s, releases with `pg_advisory_unlock` + closes the connection when the handler returns). This serializes concurrent requests for the same key so a losing request's replay lookup always finds the winner's already-committed row instead of racing it (e.g. a limited coupon's `PerUserLimit` would otherwise reject the 2nd request instead of replaying). Lock acquisition timing out → 409 `Orders.IdempotencyKeyInProgress`. Requests without a key never acquire the lock. The unique `(UserId, Key)` index + `DuplicateIdempotencyKeyException` handling (Postgres `23505`, translated by `OrdersDbContext.SaveChangesAsync`) remains as a fallback: the handler catches it, releases any coupon/gift reservations it had speculatively taken, then re-reads the key row and replays the winner's order (or returns `Orders.IdempotencyKeyReused` if the fingerprint differs).
- Controller (`OrdersController.Place`) reads the header via `[FromHeader(Name = "Idempotency-Key")]`, passes it on `PlaceOrderCommand`, and sets the `Idempotent-Replayed` response header when the handler reports a replay — the 200 body (`OrderDto`) is unchanged either way.

## Reservation arriving after cancel (W11 rework)

`InventoryReservedIntegrationEventHandler` used to no-op whenever the order was no longer `PendingStock`,
treating "already cancelled" the same as "already processed". That silently leaked stock forever when a
delayed `OrderCreated` retry reserved stock for an order the customer had already cancelled in the
meantime (`InventoryReservationExpired`/`PaymentFailed`-free leak — nothing else ever asks Inventory to give
it back). Now, when the order is specifically `Cancelled`, the handler publishes
`OrderReservationReleaseRequestedIntegrationEvent(OrderId, Reason)` instead — see
[11-inventory.md](../11-inventory.md) for the Inventory-side release handler. Every other non-`PendingStock`
status is still treated as a normal already-processed no-op.

## Stock pre-check on place order (W11)

Before placing the order (always, regardless of the key), the handler checks `IStockAvailabilityReader.GetAvailableAsync` for every **paid** cart line (gift lines are unaffected — they already have their own drop-if-insufficient check). If available < quantity for any product → 409 `Orders.InsufficientStock` with the unavailable product names, no order created.

This is a **soft** check — the authoritative reservation is still the async Inventory step, so an order can still end up `Cancelled` ("Hết hàng") if stock is depleted by a concurrent order between this check and the real reservation.

`OrderCreated` is also consumed by Cart (`ZIONShop.Cart.Application.IntegrationEvents.OrderCreatedClearCartHandler`) to clear the user's cart once an order is placed.

## Outbox (Application/Infrastructure split)

`PlaceOrderCommandHandler` et al. do **not** inject `IOutbox<OrdersDbContext>` directly (that would require the Application layer to reference the Infrastructure-only `OrdersDbContext` type, breaking the dependency direction). Instead `IOrdersUnitOfWork` (Application) exposes `AddOutboxEvent(IntegrationEvent)`, implemented by `OrdersDbContext` via an internal `Outbox<OrdersDbContext>` composed over itself. `OrdersInfrastructureExtensions` still calls `services.AddOutbox<OrdersDbContext>()` so the generic `OutboxPublisherService<OrdersDbContext>` background service runs and actually publishes queued rows to `IEventBus`.

## Docs to load

`10-event-driven`, `11-inventory`, `08-api`, `05-module-layout`
