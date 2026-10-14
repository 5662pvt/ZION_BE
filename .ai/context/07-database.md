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

## Related docs

- Inventory: [11-inventory.md](11-inventory.md)
- Handle `DbUpdateConcurrencyException` → HTTP 409: [08-api.md](08-api.md)
