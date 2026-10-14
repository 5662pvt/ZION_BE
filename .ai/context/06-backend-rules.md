# 06 — Backend Rules & Naming

## Required stack (MUST)

- MediatR (Commands/Queries)
- FluentValidation
- `Result<T>` — no throw for expected business failures
- AutoMapper
- Repository pattern (module-scoped)
- Domain Events + Integration Events
- `CancellationToken` on all async handlers

## Forbidden (MUST NOT)

- Fat controllers
- `DbContext` outside Infrastructure
- Static service classes
- Circular project references
- Duplicate business logic across modules
- Generic repository for every entity without need

## Naming convention (MUST)

| Artifact | Pattern | Example |
|----------|---------|---------|
| Command | `{Verb}{Entity}Command` | `CreateProductCommand` |
| Query | `Get{Entity}Query` / `Search{Entity}Query` | `GetProductQuery` |
| Handler | `{Name}Handler` | `CreateProductCommandHandler` |
| Validator | `{Command}Validator` | `CreateProductCommandValidator` |
| DTO | `{Entity}Dto` | `ProductDto` |
| Response DTO | `{Entity}ResponseDto` | `OrderResponseDto` |
| Domain Event | `{Entity}{PastTense}Event` | `OrderCreatedEvent` |
| Integration Event | `{Entity}{PastTense}IntegrationEvent` | `PaymentCompletedIntegrationEvent` |
| Controller | `{Entities}Controller` | `ProductsController` |
| Repository | `I{Entity}Repository` | `IProductRepository` |

## Feature folder (MUST)

One folder per use case:

```
Features/CreateProduct/
├── CreateProductCommand.cs
├── CreateProductCommandHandler.cs
└── CreateProductCommandValidator.cs
```

## Foundation — build before business modules

1. Global exception middleware
2. `ApiResponse<T>` wrapper
3. `Result<T>` in SharedKernel
4. MediatR pipeline (validation, logging)
5. JWT + refresh token infrastructure
6. Event bus abstraction + RabbitMQ
7. Serilog + correlation ID

## SharedKernel (MUST include)

```
BuildingBlocks/SharedKernel/
├── BaseEntity.cs
├── AuditableEntity.cs
├── AggregateRoot.cs
├── Result.cs / Result<T>.cs
├── DomainEvent.cs
├── IDomainEventDispatcher.cs
└── IRepository.cs
```

## Related docs

- API mapping: [08-api.md](08-api.md)
- Roadmap step 1–2: [12-roadmap.md](12-roadmap.md)
