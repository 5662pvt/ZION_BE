# Module — Payments (Phase 2, implemented W5)

## Bounded context

Payment intents against a fake gateway, provider webhooks, payment status. Owns a read-model projection of
order payability (`PayableOrder`) so it never queries the Orders module's database directly.

## Key entities

- `Payment` (aggregate root): `OrderId`, `UserId`, `Amount`, `Currency`, `Provider` ("Fake"), `IntentId` (unique),
  `IdempotencyKey` (unique per `UserId`), `Status` (`Pending|Succeeded|Failed|Cancelled`), `FailureReason?`,
  `ExpiresAt` (= `CreatedDate` + 15 min), `CompletedAt?`. Concurrency: PostgreSQL `xmin` system column via the
  same explicit `Property<uint>("xmin")...IsRowVersion()` pattern as `OrderConfiguration` (non-obsolete, unlike
  Inventory's `UseXminAsConcurrencyToken()`). Methods `Succeed`/`Fail`/`Cancel` all require `Status == Pending`
  and return `Result`, never throw.
- `PayableOrder` (projection, keyed by `OrderId` not a Payments-generated id): `UserId`, `Amount`, `Currency`,
  `State` (`AwaitingStock|Payable|Paid|Cancelled`), `UpdatedAt`. Only created for `PaymentMethod == "Online"`
  orders — a COD order never gets a row, so any Payments lookup for one reports `Payments.OrderNotFound`.
- `ProcessedWebhookEvent`: `EventId` (PK), `ProcessedAt` — webhook idempotency marker, keyed on the fake
  gateway's `eventId`, independent of `Payment.Id`/`IntentId`.

## Features (W5)

- `CreatePaymentIntent(orderId, idempotencyKey)` — same `Idempotency-Key` (per user) returns the existing
  payment; otherwise at most one `Pending` payment per order is created (an existing one is returned instead
  of creating a duplicate). Errors: projection missing/not owner → `Payments.OrderNotFound` (404); still
  `AwaitingStock` → `Payments.OrderNotPayable` (409); `Cancelled` → `Payments.OrderCancelled` (400); `Paid` →
  `Payments.OrderAlreadyPaid` (409).
- `GetPaymentIntent(intentId)` — owner-only, 404 otherwise.
- `SimulateFakePayment(intentId, result)` — `[Authorize]`, Development-only (404 everywhere else, checked via
  injected `IHostEnvironment`, not DI-time); builds a webhook payload with a fresh `eventId`, signs it with
  `FakePaymentGateway.SignPayload`, then runs it through the exact same `FakeWebhookProcessor.ProcessAsync`
  pipeline as the real webhook endpoint.
- Fake webhook endpoint — see REST contract below; delegates to `FakeWebhookProcessor` for signature
  verification, event-id idempotency, and the Pending→Succeeded/Failed transition.

## Invariants / state machine

- `PayableOrder`: `AwaitingStock → Payable → Paid`, or `{AwaitingStock,Payable} → Cancelled` (terminal; cannot
  leave `Paid`/`Cancelled`). All transition methods are idempotent no-ops on an invalid transition (`Result`
  failure swallowed by the event handler, never thrown).
- `Payment`: `Pending → {Succeeded,Failed,Cancelled}`, terminal. A payment can only be created while its
  order's projection is `Payable`.
- Webhook/simulate processing (`FakeWebhookProcessor.ProcessAsync`) rejects (no state change, no outbox event)
  when the projection is `Cancelled` or the payment is not `Pending` — covers the race where the order was
  cancelled after the intent was created but before the payment resolved.

## Events

- Consume (read-model projection, `IntegrationEvents/*Handler.cs`, all idempotent):
  - `OrderCreatedIntegrationEvent` — creates the `AwaitingStock` projection row, only when `PaymentMethod == "Online"`.
  - `InventoryReservedIntegrationEvent` — `AwaitingStock → Payable`; no-op if no projection exists (COD order).
  - `OrderCancelledIntegrationEvent`, `InventoryReservationExpiredIntegrationEvent`,
    `InventoryReservationFailedIntegrationEvent` — all three retire the projection to `Cancelled` and cancel
    any still-`Pending` payment for that order (`CancelPayableOrderHelper`). All three are subscribed
    separately because Orders does **not** publish `OrderCancelledIntegrationEvent` for a reservation that was
    never made (see `OrdersApplication.IntegrationEvents.InventoryReservationFailedIntegrationEventHandler`) —
    Payments cannot rely on that event alone to retire an `AwaitingStock` row.
- Publish (via `FakeWebhookProcessor`, transactional outbox `AddOutbox<PaymentsDbContext>()`):
  `PaymentCompletedIntegrationEvent(OrderId, PaymentId, Amount, "Fake")` on `succeeded`,
  `PaymentFailedIntegrationEvent(OrderId, PaymentId, Reason)` on `failed`. Orders reacts to both (marks the
  order `Paid`/`Cancelled` and, for completion, re-publishes `OrderConfirmedIntegrationEvent` so Inventory
  commits the reservation the same way it does for a confirmed COD order).
- Application code never references the concrete `PaymentsDbContext` directly from a handler constructor —
  it depends on `IPaymentsUnitOfWork` (`AddOutboxEvent` + `SaveChangesAsync`), implemented directly by
  `PaymentsDbContext` (same single-interface pattern as `IOrdersUnitOfWork`, not the split
  `IInventoryUnitOfWork`/`IInventoryOutbox` pattern Inventory uses).

## Fake gateway

`IPaymentGateway` (Application) / `FakePaymentGateway` (Infrastructure), configured under `Payments:Fake`
(`WebhookSecret`, `CheckoutBaseUrl`, default `http://localhost:3000`):

- `CreateIntentAsync(payment)` → `IntentId` = `"fake_" + 24 lowercase hex chars` (random, not derived from the
  payment), `RedirectUrl` = `{CheckoutBaseUrl}/checkout/pay/{intentId}` (recomputed on every read via
  `BuildRedirectUrl`, never persisted).
- `SignPayload`/`VerifySignature` — HMAC-SHA256 over the raw request body, hex-encoded,
  `CryptographicOperations.FixedTimeEquals` for the comparison.
- Startup fails fast (`PaymentsInfrastructureExtensions.AddPaymentsInfrastructure`, eager
  `Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")` read, same idea as
  `AuthBuildingBlockExtensions` reading `JwtOptions` eagerly) if `WebhookSecret` is empty outside Development.

## REST contract (W5)

- `POST /api/v1/payments/intents` `[Authorize]` — header `Idempotency-Key` (required), body `{ orderId }` →
  `PaymentIntentDto { paymentId, intentId, orderId, status, amount, currency, redirectUrl, expiresAt }`.
- `GET /api/v1/payments/intents/{intentId}` `[Authorize]`, owner-only → `PaymentIntentDto`.
- `POST /api/v1/payments/fake/{intentId}/simulate` `[Authorize]`, owner-only, Development only — body
  `{ result: "success"|"failure" }` → `PaymentIntentDto` (final status).
- `POST /api/v1/payments/webhooks/fake` `[AllowAnonymous]` — header `X-Fake-Signature`, body
  `{ eventId, intentId, result: "succeeded"|"failed", reason? }`. Contract is deliberately only ever
  `200`/`401`: the controller buffers the raw body (`Request.EnableBuffering()`) to verify the signature
  against the exact bytes received, maps a bad signature to 401, and acks everything else (unknown intent,
  already-processed `eventId`, order cancelled meanwhile, ...) with 200 so a real provider wouldn't retry a
  delivery that will never succeed — the granular `Payments.*` error code is still observable by calling the
  use case directly (unit tests) or via `SimulateFakePayment`, which does propagate it.

## DbContext

`PaymentsDbContext` — schema `payments`, migration `InitialPayments` (tables `Payments`, `PayableOrders`,
`ProcessedWebhookEvents`, `outbox_messages`). No dev seeder (there is nothing to seed; rows only ever come from
integration events or the API).

## Docs to load

`10-event-driven`, `09-security`, `08-api`, `11-inventory` (confirm flow), [modules/orders.md](orders.md)
