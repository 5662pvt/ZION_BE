# Dev-only test hooks (E2E / load tests / failure labs)

Three Development-only hooks exist purely to support external test tooling (E2E suites, load tests,
failure-injection labs). None of them affect Staging/Production.

## Seeded test customers (`AuthDbSeeder`)

Besides `admin@zionshop.local` / `staff@zionshop.local`, the Development seeder always creates
`customer@zionshop.local` / `Customer@123` (role `Customer`). Setting `Seed:LoadTestCustomers=N`
(0–500, default 0) additionally seeds `load1@zionshop.local`..`loadN@zionshop.local` / `Load@123`.
All are pre-confirmed and behave like normally-registered customers. The Users-module profile row
for any of them is **not** seeded directly — it's created lazily by `UserProfileProvisioner.EnsureAsync`
on first `GET /users/me` (or address/wishlist) call, the same path real registered users go through;
Cart/Orders don't need a Users profile to exist at all (they key purely off the Auth user id).

## Cache toggle (`Caching:Enabled`)

Default `true`. When `false`, `AddZionCaching` registers `NullDistributedCacheService` and skips
creating an `IConnectionMultiplexer` entirely — no Redis connection is attempted. `RedisHealthCheck`
already treats a missing `IConnectionMultiplexer` as healthy ("not configured"); nothing else in the
codebase resolves `IConnectionMultiplexer` directly.

## Fault injection: crash before ACK (`FaultInjection:CrashBeforeAckEventType`)

See `docs/delivery-semantics.md` ("Fault injection: crash trước ACK") for the full mechanics and how
it proves the inbox dedupe. Short version: set this to an event type's short class name (e.g.
`OrderCreatedIntegrationEvent`); in Development only, `RabbitMqConsumerService` runs
`Environment.FailFast` right after that message's handlers succeed but before `BasicAck`, so it's
redelivered on restart without the flag.
