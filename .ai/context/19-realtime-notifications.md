# 19 — Real-time order/payment notifications (W18 foundation wave)

New feature on top of the existing `NotificationLog` email-log module -- two independent layers:

## In-app notification feed (persisted)

- `UserNotification` (Notifications.Domain) -- separate entity from `NotificationLog`, table
  `UserNotifications` (schema `notifications`). Types: `OrderPlaced, OrderConfirmed, OrderShipped,
  OrderCancelled, PaymentCompleted, PaymentFailed`.
- Written by integration-event handlers in `ZIONShop.Notifications.Application/IntegrationEvents/`:
  the 3 existing order-status handlers now also write a `UserNotification` (independent of email
  deliverability), plus 3 new handlers (`OrderCreatedNotificationHandler`,
  `PaymentCompletedNotificationHandler`, `PaymentFailedNotificationHandler`) that are in-app only (no
  email, no `NotificationLog` row). All registered via `AddIntegrationEventHandler<,,NotificationsDbContext>`
  (inbox dedupe) -- no manual idempotency checks in the new handlers.
- REST feed, additive, under `api/v1/notifications/feed` (`NotificationsFeedController`): paged list,
  unread-count, mark-one-read, mark-all-read. Does not touch the existing `/notifications/mine` or
  `/admin/notifications` email-log viewers.

## Real-time push (ephemeral, Redis pub/sub relay)

Why a relay instead of calling SignalR directly from the handler: notification *creation* happens
wherever the integration-event consumer runs (Worker process in docker-compose, since
`BackgroundProcessing:Enabled=false` on the Api there; Api process in plain local `dotnet run`).
Real-time *delivery* to the browser must happen from whichever process holds the SignalR connection --
always the Api host. Two different processes in the deployed topology, so creation and delivery are
decoupled via Redis pub/sub (channel `zion:notifications:{userId}`), NOT SignalR's official Redis
backplane (that synchronizes multiple Hub instances -- a different problem; there is only ever one Api
instance here).

- `ZIONShop.Realtime` (new BuildingBlock): `INotificationPush` / `RedisNotificationPublisher`, fail-open
  by contract (a Redis outage must never throw out of a handler or block/fail the business transaction --
  bounded ~500ms budget, catches everything, logs a Warning). Own `IConnectionMultiplexer`, independent of
  `ZIONShop.Caching` (works even when `Caching:Enabled=false`). `AddZionRealtime(configuration)` called in
  both `ZIONShop.Api` and `ZIONShop.Worker` `Program.cs` (publish side must exist wherever handlers run).
- `NotificationsHub` (`/hubs/notifications`, Api-only, `[Authorize]`) + `ClaimsUserIdProvider` (resolves
  the same Sub/NameIdentifier claim as `CurrentUserService`, so `Clients.User(userId)` matches).
- `NotificationRelayHostedService` (Api-only `BackgroundService`, registered unconditionally -- not
  gated by `BackgroundProcessing:Enabled`): subscribes to `zion:notifications:*`, forwards each message
  to `Clients.User(userId).SendAsync("notificationReceived", dto)`. Redis down at startup/mid-flight
  degrades to "no real-time push" (clients fall back to polling the feed REST endpoints); retries with a
  fixed backoff; never crashes the Api host.
- Browser SignalR clients can't set an `Authorization` header on the handshake -- `AuthBuildingBlockExtensions`
  bridges a `?access_token=` query param into the JWT bearer token, scoped to paths under `/hubs` only
  (`OnMessageReceived`); the normal header path for REST is unaffected.
- `deploy/nginx/nginx.conf` has a `/hubs/` location with `Upgrade`/`Connection: upgrade` and a long
  `proxy_read_timeout` for the docker-compose/nginx topology.

FE contract (next wave): SignalR event `notificationReceived` on hub path `/hubs/notifications`, payload
shape `UserNotificationPushDto { Id, Type, Title, Message, OrderId, CreatedAt, IsRead }`.
