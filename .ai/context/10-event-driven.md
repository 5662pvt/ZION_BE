# 10 — Event-Driven Communication

## Event types

| Type | Scope | Transport |
|------|-------|-----------|
| Domain Event | Inside module/aggregate | In-process (MediatR) |
| Integration Event | Cross-module | RabbitMQ |

## Checkout flow (reference)

```
OrderCreated (Orders)
    → InventoryReserved (Inventory)
    → PaymentRequested (Payments)
    → PaymentCompleted (Payments)
    → OrderConfirmed (Orders)
    → SendEmailRequested (Notifications)
```

## Rules (MUST)

- Publish integration events from **Application** after successful commit
- Consumers in target module **Application** handlers
- Contracts in `BuildingBlocks/Contracts`
- **MUST NOT** call another module's service/repository/DbContext directly

## Naming

`{Aggregate}{PastTense}IntegrationEvent`

Examples: `OrderCreatedIntegrationEvent`, `PaymentCompletedIntegrationEvent`.

## RabbitMQ topology

Declared idempotently by `RabbitMqTopology.Declare` (`BuildingBlocks/ZIONShop.EventBus/RabbitMq/`),
called by both the publisher (`RabbitMqPublisher`, lazily on first publish) and the consumer
(`RabbitMqConsumerService.Initialize`), so they can never disagree on names/arguments:

```
{Exchange} (topic)                         {Exchange}.dlx (direct)
     |  bind per registered event type           |  bind routing-key = {QueueName}
     v                                            v
{QueueName}  --nack(requeue:false)-- or --------> {QueueName}.dlq
     ^            x-dead-letter-* on the queue
     |  TTL expiry, x-dead-letter-* -> default exchange "" , routing key = {QueueName}
     |
{QueueName}.retry.1 .. {QueueName}.retry.{RetryCount}   (x-message-ttl = RetryDelaysSeconds[n])
```

- **Main queue** (`{QueueName}`): `x-dead-letter-exchange = {Exchange}.dlx`,
  `x-dead-letter-routing-key = {QueueName}` — anything nacked with `requeue:false` lands in the DLQ.
- **Retry queues** (`{QueueName}.retry.1..N`, `N = EventBus:RetryCount`): no consumer ever attaches
  to these. A message sits there until `x-message-ttl` (from `EventBus:RetryDelaysSeconds[n]`,
  seconds, last value reused for attempts beyond the array) expires, then is dead-lettered via the
  **default exchange** back onto `{QueueName}` for redelivery.
- **DLQ** (`{QueueName}.dlq`): terminal. Messages here were either unroutable/poison on first
  receipt, or exhausted `RetryCount` retries. RabbitMQ adds an `x-death` header automatically.
- **One-time legacy queue migration**: environments from before this topology have `{QueueName}`
  declared with no arguments. Redeclaring it with the new `x-dead-letter-*` args fails with
  `406 PRECONDITION_FAILED`, which closes the channel. `RabbitMqTopology` catches that, opens a
  fresh channel, deletes the (expected-empty) queue, and redeclares it correctly. If the queue is
  **not** empty, it throws instead of silently dropping messages — drain or delete the queue once
  by hand, then retry.

## Publisher confirms vs. consumer ack vs. redelivery

- **Publish**: `RabbitMqPublisher` holds a single long-lived channel in confirm mode
  (`ConfirmSelect`) guarded by a semaphore. Every publish is `mandatory: true` for routing keys with
  a registered consumer, waits on `WaitForConfirmsOrDie(EventBus:ConfirmTimeoutSeconds)`, and throws
  if the broker nacks/times out **or** returns the message as unroutable (`Basic.Return`). A
  publisher confirm only proves the broker accepted the message into a queue somewhere -- it says
  nothing about whether a handler ever processed it.
- **Broker down vs. publish rejected**: connectivity failures (broker unreachable, connection/channel
  closed, confirm nack/timeout from `WaitForConfirmsOrDie` -- those close the channel too) are
  reported as `BrokerUnavailableException`, distinct from a publish that reached the broker but was
  rejected for a non-transport reason (unroutable, unknown/undeserializable event type, a plain
  exception). `OutboxPublisherService` treats them differently -- see below -- so a RabbitMQ outage
  can never silently drain the outbox's retry budget. The underlying connection (`RabbitMqConnection`)
  is shared by the publisher and the consumer; while it is auto-recovering from a drop (not open, but
  not explicitly closed either), both `TryGetConnection` and `GetConnection` wait for that recovery
  instead of opening a second, leaked connection.
- **Consumer ack**: the consumer runs **every** registered handler for the event type, even if an
  earlier one throws -- each handler has its own try/catch, so one failing handler never prevents a
  sibling handler from applying its effect on this delivery (it would otherwise have to wait for the
  failing handler to become healthy, since both would be re-invoked together on the next redelivery).
  Only after all handlers have run does the consumer ack (if none failed) or nack (if any did) --
  which is what drives retry/DLQ below. Delivery is at-least-once; handlers must be idempotent, keyed
  on `IntegrationEvent.EventId` (`IIntegrationEventHandler<T>`). A failing `BasicAck`/`BasicNack` call
  itself (e.g. the channel just closed) is logged and swallowed, not treated as a handler failure --
  the broker simply redelivers.
- **Retry vs. redelivery**: a transient handler failure is not simply requeued on the main queue (that
  would spin the consumer hot against a broker/dependency that just failed). Instead the consumer
  publishes a copy to `{QueueName}.retry.{attempt}` (carrying `x-retry-count` and a truncated
  `x-last-error` header) through the same confirmed publisher, then acks the original. The retry
  queue's TTL provides the delay; expiry redelivers to the main queue. A message coming back this
  way has routing key `{QueueName}` (not the original event name), so the consumer resolves the
  event type from `BasicProperties.Type` first, falling back to the routing key only for a first
  delivery that somehow lacks it. If the republish to the retry queue itself fails, the consumer waits
  1s then nacks with `requeue:true` -- without that delay, a broker outage would otherwise hot-loop
  redeliver-and-fail the same message against the main queue.
- **Consumer cancelled by the broker** (e.g. the queue was deleted/redeclared by another instance
  during the one-time 406 migration): logged Critical, then the consumer reconnects, redeclares the
  topology and resumes consuming with the same exponential backoff used at startup, instead of
  silently going idle.
- **Outbox failure handling** (`OutboxPublisherService<TDbContext>`):
  - `BrokerUnavailableException` -> stop the batch immediately, do **not** increment `Attempts` for
    that message or any remaining one in the batch, log a single Warning. The next poll
    (`EventBus:OutboxPollSeconds`) retries the whole batch once the broker is back.
  - Any other publish failure (confirm nack/timeout, unroutable, unknown/undeserializable event
    type) -> `Attempts++`, `Error` set, continue with the next message. `ProcessedOn` is set only
    after the broker has confirmed the publish.
  - `Attempts >= OutboxPublisherService<T>.MaxAttempts` (10) with `ProcessedOn == null` is
    considered **failed** and logged Critical -- listed and retriable via the admin API below.

## Inspecting the DLQ

RabbitMQ management UI: `http://localhost:15672` (`deploy/docker-compose.yml` exposes it; same
`EventBus__User`/`EventBus__Pass` credentials as the app). Queues tab -> `{QueueName}.dlq` -> Get
Message(s) to inspect payload, `x-death` (why/when it was dead-lettered), and `x-last-error`/
`x-retry-count` headers left by the consumer's last retry attempt.

## Admin "Hệ thống" API (outbox / inbox / DLQ / worker health)

`AdminSystemController` (`api/v1/admin/system`, `Policies.AdminOnly`) exposes the outbox/inbox tables
and the DLQ without the Admin/API layer ever referencing a module's Infrastructure project:

- `GET /modules` -- outbox/inbox module names, derived from each DbContext type name
  (`ModuleNameResolver.FromDbContext`, e.g. `OrdersDbContext` -> `"Orders"`).
- `GET /outbox` / `GET /outbox/{module}/{id}` / `POST /outbox/{module}/{id}/retry` /
  `POST /outbox/retry-failed` -- backed by `IOutboxAdminStore` (`EventBus/Outbox/`), one instance per
  module registered automatically by `AddOutbox<TDbContext>()`. Status (`Pending`/`Failed`/`Processed`)
  uses the same `Attempts`/`ProcessedOn` classification as `OutboxPublisherService<T>.MaxAttempts`.
  Retry resets `Attempts`/`Error` so the next outbox poll republishes the row.
- `GET /inbox` -- backed by `IInboxAdminStore` (`EventBus/Inbox/`), registered per module via
  `AddInboxAdmin<TDbContext>()` (called once from each module's `Add<Module>Infrastructure`, since
  nothing else implies which modules have an inbox table the way `AddOutbox` does for outbox).
- `GET /dlq`, `POST /dlq/requeue`, `DELETE /dlq/{messageId}` -- backed by `IDeadLetterAdmin`
  (`EventBus/RabbitMq/RabbitMqDeadLetterAdmin.cs`, `EventBus/InMemory/InMemoryDeadLetterAdmin.cs`), its
  own short-lived channel on the existing `RabbitMqConnection`. Peek does `BasicGet(autoAck:false)` then
  `BasicNack(requeue:true)` for everything it read, so peeking never removes a message but does set its
  AMQP "redelivered" flag and may reorder the queue. Requeue republishes via
  `RabbitMqPublisher.PublishRequeuedAsync` (drops `x-death`/`x-retry-count`/`x-last-error`, keeps
  `traceparent`/`tracestate`). Broker unreachable -> 503 `System.BrokerUnavailable`.
- Outbox/DLQ payloads are masked by the shared `PayloadMasker` (`EventBus/Admin/`) before they reach the
  response: any JSON key containing (case-insensitively) email/phone/recipientname/streetaddress/
  address/password/token/secret/card is replaced with `"***"`.
- `GET /health` (admin) gained a `worker` check (`WorkerHealthCheck`,
  `BuildingBlocks/ZIONShop.HealthChecks/`): Healthy `"consumers=N"` when the main queue has >= 1
  consumer, Unhealthy `"no consumer: start the worker"` at 0, Healthy `"in-memory provider"` under the
  InMemory provider. Registered without the `"ready"` tag, so it is excluded from `/health/ready`
  (Program.cs's `Predicate = check => check.Tags.Contains("ready")`) -- whether anyone is running the
  Worker is an operational fact about the deployment, not this API instance's own readiness, so it
  must never flip a load balancer's health probe. The admin controller's `/health` action has no tag
  predicate at all, so it still surfaces `worker` alongside `postgres`/`redis`/`rabbitmq`.

## Configuration (`EventBus:*`, see `RabbitMqOptions`)

| Key | Default | Meaning |
|-----|---------|---------|
| `RetryCount` | 5 | Delayed retries before dead-lettering (message-level, not connection) |
| `RetryDelaysSeconds` | `[1,5,15,30,60]` | Delay for retry attempt N (last value reused beyond the array) |
| `ConnectRetryCount` | 5 | Exponential-backoff attempts to (re)establish the broker connection -- separate from `RetryCount` |
| `PrefetchCount` | 20 | Consumer QoS (max unacked deliveries in flight) |
| `ConfirmTimeoutSeconds` | 5 | How long a publish waits for the broker's confirm |
| `OutboxPollSeconds` | 2 | Outbox polling interval |

## Tracing & metrics

`ZIONShop.EventBus.Diagnostics.MessagingDiagnostics` (`BuildingBlocks/ZIONShop.EventBus/Diagnostics/`)
is the single `ActivitySource`/`Meter` (both named `ZIONShop.Messaging`) every piece of the bus reports
into. This building block takes **no OpenTelemetry SDK dependency** -- only `System.Diagnostics`/
`System.Diagnostics.Metrics` -- the host wires an actual exporter against these names separately (see
`docs/observability.md`).

### Span names and propagation

| Span | Kind | Where | Parent |
|---|---|---|---|
| `{EventType} publish` | Producer | `OutboxPublisherService`, around each `IEventBus.PublishAsync` dispatch | `OutboxMessage.TraceParent`/`TraceState` (captured by `Outbox<T>.Add` from `Activity.Current` at the time the business code added the event) -- new root if absent |
| `{EventType} process` | Consumer | `RabbitMqConsumerService.OnMessageReceivedAsync`, around the whole delivery | `traceparent`/`tracestate` headers on the RabbitMQ message |
| `{HandlerName}` | Internal | once per handler, both `RabbitMqConsumerService` and `InMemoryEventBus` | the enclosing `{EventType} publish`/`process` activity (`Activity.Current`) |

`RabbitMqPublisher.Publish` is the single place that writes the `traceparent`/`tracestate` headers,
from whatever `Activity.Current` is at publish time -- this covers both a fresh publish from the outbox
and a consumer's retry-queue republish (`PublishToRetryQueueAsync` also copies the failed delivery's own
headers forward as a fallback baseline, so context survives even if no activity happens to be current).
A retried message therefore keeps the same `TraceId` across every attempt. `Activity.DefaultIdFormat` is
forced to W3C in `MessagingDiagnostics`'s static constructor so `Activity.Current?.Id` is always a
parseable `traceparent` string, independent of whether the process is hosted by ASP.NET Core.

Failures are recorded on the relevant activity via `MessagingDiagnostics.RecordException` (OTel
`exception` event + `ActivityStatusCode.Error`), at the outbox publish, the consumer's overall
delivery, and each handler.

### Metrics

| Instrument | Type | Tags | Meaning |
|---|---|---|---|
| `messaging.published` | Counter<long> | `event.type` | Outbox successfully dispatched an event via `IEventBus` |
| `messaging.consumed` | Counter<long> | `event.type` | A delivery finished (every handler ran, RabbitMQ) / a publish's handlers all ran (InMemory) |
| `messaging.handler.failed` | Counter<long> | `event.type`, `consumer` | A single handler threw |
| `messaging.retried` | Counter<long> | `event.type` | A failed delivery was republished to a retry queue |
| `messaging.dead_lettered` | Counter<long> | `event.type` | A message was nacked without requeue (unroutable type, poison body, or retries exhausted) |
| `messaging.handler.duration` | Histogram<double> (ms) | `event.type`, `consumer`, `outcome` | Per-handler invocation time |
| `outbox.pending` | ObservableGauge<long> | `module` (DbContext name) | Rows with `ProcessedOn == null && Attempts < MaxAttempts`, refreshed every `OutboxPublisherService` poll |
| `outbox.failed` | ObservableGauge<long> | `module` | Rows with `ProcessedOn == null && Attempts >= MaxAttempts`, same cadence |
| `rabbitmq.queue.messages` | ObservableGauge<long> | `queue` (`main`/`dlq`) | `QueueDeclarePassive` message count, refreshed every 15s by a background loop in `RabbitMqConsumerService`; a tick is skipped silently if the broker/queue is unavailable |

### Logging

`IntegrationEventLogScope.Begin` pushes `MessageId`/`EventType`/`OrderId` (when the event has that
property -- reflected once per event type and cached) onto the logger scope via `ILogger.BeginScope`
around each publish and each handler invocation, so every log line underneath is enrichable by them.
This building block has no package reference on Serilog; `BeginScope` is enough since Serilog's own
`ILogger` provider already surfaces scope state as `LogContext` properties.

## Related docs

- Architecture diagram: [02-architecture.md](02-architecture.md)
- Inventory reactions: [11-inventory.md](11-inventory.md)
