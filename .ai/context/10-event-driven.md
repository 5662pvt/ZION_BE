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
    considered **failed** and logged Critical -- an admin screen will list these (roadmap W14).

## Inspecting the DLQ

RabbitMQ management UI: `http://localhost:15672` (`deploy/docker-compose.yml` exposes it; same
`EventBus__User`/`EventBus__Pass` credentials as the app). Queues tab -> `{QueueName}.dlq` -> Get
Message(s) to inspect payload, `x-death` (why/when it was dead-lettered), and `x-last-error`/
`x-retry-count` headers left by the consumer's last retry attempt.

## Configuration (`EventBus:*`, see `RabbitMqOptions`)

| Key | Default | Meaning |
|-----|---------|---------|
| `RetryCount` | 5 | Delayed retries before dead-lettering (message-level, not connection) |
| `RetryDelaysSeconds` | `[1,5,15,30,60]` | Delay for retry attempt N (last value reused beyond the array) |
| `ConnectRetryCount` | 5 | Exponential-backoff attempts to (re)establish the broker connection -- separate from `RetryCount` |
| `PrefetchCount` | 20 | Consumer QoS (max unacked deliveries in flight) |
| `ConfirmTimeoutSeconds` | 5 | How long a publish waits for the broker's confirm |
| `OutboxPollSeconds` | 2 | Outbox polling interval |

## Related docs

- Architecture diagram: [02-architecture.md](02-architecture.md)
- Inventory reactions: [11-inventory.md](11-inventory.md)
