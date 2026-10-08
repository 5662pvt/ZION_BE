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

## REST contract (W4/W5)

- `POST /api/v1/payments/intents` `[Authorize]` — header `Idempotency-Key`, body `{ orderId }` → `{ paymentId, intentId, status, redirectUrl }`
  (`redirectUrl` points to FE `/checkout/pay/{intentId}`)
- `GET /api/v1/payments/{paymentId}` `[Authorize]`
- `POST /api/v1/payments/webhooks/fake` — header `X-Fake-Signature` (HMAC-SHA256, secret `Payments:Fake:WebhookSecret`)
- `POST /api/v1/payments/fake/{intentId}/simulate` — body `{ result: "success"|"failure" }` (Development only)

## Docs to load

`10-event-driven`, `09-security`, `08-api`, `11-inventory` (confirm flow)
