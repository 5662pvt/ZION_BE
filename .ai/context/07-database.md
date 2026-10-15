# 07 — PostgreSQL & Persistence

> **PostgreSQL only.** Do not add `Microsoft.EntityFrameworkCore.SqlServer`.

## EF Core (MUST)

- Package: `Npgsql.EntityFrameworkCore.PostgreSQL` (`UseNpgsql`)
- One `DbContext` per module (`ProductsDbContext`, `OrdersDbContext`, …)
- Migrations: `Infrastructure/Persistence/Migrations/`
- Connection: `ConnectionStrings:DefaultConnection` (local docker `postgres` service; Neon for cloud)

## Auditable entity base (MUST)

```csharp
public abstract class AuditableEntity
{
    public Guid Id { get; protected set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
    public uint Xmin { get; set; }
}
```

```csharp
builder.Property(e => e.Xmin).IsRowVersion(); // Npgsql maps uint Xmin -> system column xmin
builder.HasQueryFilter(e => !e.IsDeleted);
```

PostgreSQL has no `rowversion`/`timestamp` column type like SQL Server — use the built-in `xmin` system column as the optimistic concurrency token (`UseXminAsConcurrencyToken` / `IsRowVersion()` on a `uint` property), not a custom byte array.

## Rules

| Required | Forbidden |
|----------|-----------|
| Soft delete | Business logic in SQL triggers (unless justified) |
| Auditable fields | God tables |
| EF migrations | Cascade delete on all relationships |
| `xmin` concurrency token | Massive cross-module joins |
| Transactions for checkout/payment/inventory | Negative stock |

## Schema strategy

- One Postgres schema per module: `products`, `orders`, `inventory`, …
- Cross-module references by **ID only**; eventual consistency via events
- No FK across bounded contexts where avoidable

## Concurrency (escalation order)

1. SQL transaction (`Serializable` / `RepeatableRead` when needed)
2. `xmin` optimistic concurrency token
3. Redis distributed lock (flash sales)
4. Queue for inventory reconciliation

## Docker (local)

```yaml
postgres:
  image: postgres:16-alpine
  environment:
    POSTGRES_PASSWORD: ${DB_PASSWORD}
  ports:
    - "5432:5432"
```

## Caching (Redis cache-aside)

`ZIONShop.Caching` (`IDistributedCacheService`) backs read-heavy queries in Products and Admin. `RedisCacheService` is registered by `AddZionCaching()` when Redis is reachable; otherwise (incl. the `Testing` environment, and whenever Redis is down) `NullDistributedCacheService` is used instead — every module's `Add<Module>Application()` does `TryAddSingleton<IDistributedCacheService, NullDistributedCacheService>()` so DI always resolves even without Redis.

**Fail-open, always.** Any Redis exception on get/set/remove is caught, logged as a rate-limited (30s) warning, and treated as a miss/no-op — a Redis outage never throws into a handler, it just means every read falls through to the database. The multiplexer itself uses `AbortOnConnectFail = false` with a 2s connect/sync timeout so a down Redis doesn't block requests.

`GetOrCreateAsync<T>(key, factory, ttl, ct)` is cache-aside with in-process single-flight per key (a `SemaphoreSlim` per key, double-checked after acquiring) — on a cold key, only one caller runs `factory`, the rest wait and reuse its result. Stored TTLs get ±10% jitter so many keys set together don't expire in the same instant. Only non-null values are cached — callers pass the already-unwrapped DTO, never a `Result` failure, so failures/not-found are never cached.

**Stock is never cached.** `ProductDto` has no stock/rating fields by design — those stay live reads.

### Keys and TTLs

| Area | Key | TTL | Built by |
|---|---|---|---|
| Product detail (by id) | `zion:products:detail:id:{id}` | 5 min | `ProductCacheKeys.DetailById` |
| Product detail (by slug) | `zion:products:detail:slug:{slug}` | 5 min | `ProductCacheKeys.DetailBySlug` |
| Category tree | `zion:categories:tree` | 30 min | `ProductCacheKeys.CategoryTree` |
| Admin reports | `zion:reports:{query}:{from}:{to}:{...params}` | 60 s | `ReportCacheKeys.*` (Admin.Application) |

Only **Published** products are cached under the detail keys (today's not-published → `NotFound` is never cached, so there's nothing to invalidate on create).

### Invalidation table

| Handler | Keys removed |
|---|---|
| `UpdateProduct` | detail id + detail slug (slug is immutable on update, so there's only one slug) |
| `ArchiveProduct` / `PublishProduct` | detail id + detail slug |
| `AddProductImage` / `RemoveProductImage` / `ReorderProductImages` | detail id + detail slug |
| `CreateProduct` | none (nothing was ever cached for it) |
| Category `Create` / `Update` / `Delete` | `zion:categories:tree` |
| Admin reports | never explicitly invalidated — 60s TTL only |

`ProductDto.CategoryName` is denormalized at read time, so renaming a category can leave a stale name on an already-cached product detail for up to 5 minutes. This is accepted as-is — no cache versioning scheme was built for it.

### Metrics

`System.Diagnostics.Metrics` Meter `"ZIONShop.Caching"` exposes counters `cache.hits` / `cache.misses` / `cache.errors`, tagged `cache.name` (derived from the key prefix, e.g. `products.detail`, `categories.tree`, `reports.dashboard`).

## Related docs

- Inventory: [11-inventory.md](11-inventory.md)
- Handle `DbUpdateConcurrencyException` → HTTP 409: [08-api.md](08-api.md)
