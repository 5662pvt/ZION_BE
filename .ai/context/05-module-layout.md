# 05 — Per-Module Layout (Clean Architecture)

Apply this pattern to **every** backend module. Example: **Products**.

## Domain — `{Module}.Domain/`

```
Entities/
ValueObjects/
Enums/
Events/
Repositories/       # interfaces only
Specifications/
Exceptions/
Services/             # pure domain logic
```

**MUST NOT:** EF Core, SQL, Redis, HttpClient.

## Application — `{Module}.Application/`

```
Features/
├── CreateProduct/
│   ├── CreateProductCommand.cs
│   ├── CreateProductCommandHandler.cs
│   └── CreateProductCommandValidator.cs
├── GetProduct/
├── SearchProducts/
└── Categories/
    ├── CreateCategory/
    └── GetCategories/
DTOs/
Interfaces/
Mappings/
Behaviors/            # ValidationBehavior, LoggingBehavior
Services/
```

**MUST:** CQRS, MediatR, FluentValidation.

## Infrastructure — `{Module}.Infrastructure/`

```
Persistence/
│   └── {Module}DbContext.cs
Repositories/
Configurations/
Cache/
MessageBus/
External/
```

## API

```
Controllers/          # thin — MediatR only
Endpoints/            # optional minimal APIs
DependencyInjection/
```

Controllers may live in host `Api` project instead of `{Module}.API`.

## Related docs

- Naming & stack: [06-backend-rules.md](06-backend-rules.md)
- Database: [07-database.md](07-database.md)
