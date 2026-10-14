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
- `InventoryReservationFailed` → `PendingStock` → `Cancelled` ("Hết hàng"). No `OrderCancelled` is published — nothing was reserved, so there is nothing to compensate.
- `InventoryReservationExpired` → `AwaitingPayment` → `Cancelled` ("Quá hạn thanh toán") + publishes `OrderCancelled`.
- `PaymentCompleted` → `AwaitingPayment` → `Paid` + publishes `OrderConfirmed`.
- `PaymentFailed` → `AwaitingPayment` → `Cancelled` ("Thanh toán thất bại") + publishes `OrderCancelled`.

`OrderCreated` is also consumed by Cart (`ZIONShop.Cart.Application.IntegrationEvents.OrderCreatedClearCartHandler`) to clear the user's cart once an order is placed.

## Outbox (Application/Infrastructure split)

`PlaceOrderCommandHandler` et al. do **not** inject `IOutbox<OrdersDbContext>` directly (that would require the Application layer to reference the Infrastructure-only `OrdersDbContext` type, breaking the dependency direction). Instead `IOrdersUnitOfWork` (Application) exposes `AddOutboxEvent(IntegrationEvent)`, implemented by `OrdersDbContext` via an internal `Outbox<OrdersDbContext>` composed over itself. `OrdersInfrastructureExtensions` still calls `services.AddOutbox<OrdersDbContext>()` so the generic `OutboxPublisherService<OrdersDbContext>` background service runs and actually publishes queued rows to `IEventBus`.

## Docs to load

`10-event-driven`, `11-inventory`, `08-api`, `05-module-layout`
