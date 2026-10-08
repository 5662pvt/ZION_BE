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
  `{ id, code, status, paymentMethod, subtotal, shippingFee, total, currency, createdAt, shippingAddress: AddressSnapshot, lines: [{ productId, productName, unitPrice, quantity, lineTotal }] }`
- `GET /api/v1/orders/{id}` `[Authorize]` → `OrderDto`
- `GET /api/v1/orders/mine?page&pageSize&status` `[Authorize]` → paged `OrderSummaryDto { id, code, status, total, currency, createdAt, itemCount }`
- `POST /api/v1/orders/{id}/cancel` `[Authorize]` — body `{ reason? }`
- Admin `[StaffOrAdmin]`:
  - `GET /api/v1/orders?page&pageSize&status&from&to&keyword`
  - `POST /api/v1/orders/{id}/confirm|ship|complete`

Status values: `PendingStock`, `AwaitingPayment`, `Confirmed`, `Paid`, `Shipped`, `Completed`, `Cancelled`.

## Docs to load

`10-event-driven`, `11-inventory`, `08-api`, `05-module-layout`
