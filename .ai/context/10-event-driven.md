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

## Related docs

- Architecture diagram: [02-architecture.md](02-architecture.md)
- Inventory reactions: [11-inventory.md](11-inventory.md)
