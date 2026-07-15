# 07 — SQL Server & Persistence

> **Microsoft SQL Server only.** Do not add Npgsql/PostgreSQL.

## EF Core (MUST)

- Package: `Microsoft.EntityFrameworkCore.SqlServer`
- One `DbContext` per module (`ProductsDbContext`, `OrdersDbContext`, …)
- Migrations: `Infrastructure/Persistence/Migrations/`
- Connection: `ConnectionStrings:DefaultConnection`

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
    public byte[] RowVersion { get; set; }
}
```

```csharp
builder.Property(e => e.RowVersion).IsRowVersion();
builder.HasQueryFilter(e => !e.IsDeleted);
```

## Rules

| Required | Forbidden |
|----------|-----------|
| Soft delete | Business logic in SQL triggers (unless justified) |
| Auditable fields | God tables |
| EF migrations | Cascade delete on all relationships |
| `RowVersion` concurrency | Massive cross-module joins |
| Transactions for checkout/payment/inventory | Negative stock |

## Schema strategy

- One SQL schema per module: `products`, `orders`, `inventory`, …
- Cross-module references by **ID only**; eventual consistency via events
- No FK across bounded contexts where avoidable

## Concurrency (escalation order)

1. SQL transaction (`Serializable` / `RepeatableRead` when needed)
2. `RowVersion` optimistic concurrency
3. Redis distributed lock (flash sales)
4. Queue for inventory reconciliation

## Docker (local)

```yaml
sqlserver:
  image: mcr.microsoft.com/mssql/server:2022-latest
  environment:
    ACCEPT_EULA: Y
    MSSQL_SA_PASSWORD: ${DB_PASSWORD}
  ports:
    - "1433:1433"
```

## Related docs

- Inventory: [11-inventory.md](11-inventory.md)
- Handle `DbUpdateConcurrencyException` → HTTP 409: [08-api.md](08-api.md)
