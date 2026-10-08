# Module — Payments (Phase 2)

## Bounded context

Payment intents, provider webhooks, payment status.

## Key entities (suggested)

- `Payment`, `PaymentIntent`, `PaymentStatus` enum

## Features (minimum Phase 2)

- `CreatePaymentIntent`
- `ConfirmPayment` / webhook handler
- `GetPaymentStatus`

## Events

- Consume: `PaymentRequestedIntegrationEvent` (or triggered after inventory reserved)
- Publish: `PaymentCompletedIntegrationEvent`, `PaymentFailedIntegrationEvent`

## Quyết định checkout MVP (2026-10-08)

MVP dùng `FakePaymentGateway` (giả lập online payment) + webhook ký HMAC; endpoint simulate chỉ bật ở Development. Chi tiết luồng checkout: [modules/orders.md](orders.md).

## Security

Webhook signature validation; idempotency keys — see [09-security.md](../09-security.md)

## Docs to load

`10-event-driven`, `09-security`, `08-api`, `11-inventory` (confirm flow)
