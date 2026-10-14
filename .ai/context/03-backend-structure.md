# 03 — Backend Folder Structure & Modules

## Solution tree

```
ZIONShop.slnx
src/
├── BuildingBlocks/
│   ├── SharedKernel/
│   ├── Contracts/
│   ├── EventBus/
│   ├── Caching/
│   ├── Logging/
│   ├── Auth/
│   └── Common/
├── Modules/
│   ├── Auth/
│   ├── Users/
│   ├── Products/       # includes Categories
│   ├── Inventory/
│   ├── Cart/
│   ├── Orders/
│   ├── Payments/
│   ├── Promotions/
│   ├── Notifications/
│   ├── Reviews/
│   └── Admin/
└── Api/
tests/
├── Unit/
├── Integration/
└── Architecture/
```

## Module list

| Module | Bounded context | Phase |
|--------|-----------------|-------|
| Auth | Login, register, JWT, refresh | 1 |
| Users | Profiles, addresses, preferences | 1 |
| Products | Products, SKUs, **Categories**, brands | 1 |
| Cart | Guest/user cart | 1 |
| Orders | Order lifecycle, checkout | 2 |
| Inventory | Stock, reservation, release | 2 |
| Payments | Payment intents, webhooks | 2 |
| Promotions | Coupons, campaigns, gift rules | 3 |
| Reviews | Ratings | 3 |
| Notifications | Email/SMS via events | 3 |
| Admin | Reports, dashboard read-models (CRUD catalog → Products) | 4 |

## Categories (resolved)

**Categories are NOT a separate module.** They belong to **Products**:

- `Products.Domain/Entities/Category.cs`
- `Products.Application/Features/Categories/`

## Per-module projects (MUST)

```
Modules/{ModuleName}/
├── {ModuleName}.Domain/
├── {ModuleName}.Application/
├── {ModuleName}.Infrastructure/
├── {ModuleName}.API/     # optional — may use host Api
└── {ModuleName}.Tests/
```

Host `Api` references Application + Infrastructure and registers DI.

## Module-specific context

See `modules/*.md` for bounded-context details when implementing a single module.

## Related docs

- Layer folders: [05-module-layout.md](05-module-layout.md)
- BE rules: [06-backend-rules.md](06-backend-rules.md)
