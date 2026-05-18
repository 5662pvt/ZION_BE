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

## FE module

`frontend/src/modules/orders/`, `frontend/src/modules/checkout/`

## Docs to load

`10-event-driven`, `11-inventory`, `08-api`, `05-module-layout`
