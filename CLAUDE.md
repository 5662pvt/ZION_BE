# ZIONShop — AI Entry Point

> **Đọc file này trước.** Chi tiết nằm trong [`.ai/context/`](.ai/context/) — chỉ mở file liên quan task để tiết kiệm token.

**Project:** Ecommerce Enterprise Platform · .NET 8 + React/Vite · **SQL Server only** · Modular Monolith + Clean Architecture + CQRS + Events

---

## Quick rules (MUST)

| Rule | Detail |
|------|--------|
| Database | Microsoft SQL Server + EF Core — **no PostgreSQL** |
| Layers | API → Application → Domain ← Infrastructure |
| Modules | No cross-module `DbContext`; use integration events |
| Categories | Inside **Products** module, not separate |
| Code | MediatR, FluentValidation, `Result<T>`, `CancellationToken` |
| API response | `{ success, message, data, errors, pagination }` |
| Order | Domain → Application → API → tests → Frontend |

---

## Context files — chọn theo task

| # | File | Khi nào đọc |
|---|------|-------------|
| 01 | [overview](.ai/context/01-overview.md) | Bắt đầu dự án, stack, mục tiêu |
| 02 | [architecture](.ai/context/02-architecture.md) | Layers, dependency, module isolation |
| 03 | [backend-structure](.ai/context/03-backend-structure.md) | Solution tree, danh sách module |
| 04 | [frontend-structure](.ai/context/04-frontend-structure.md) | React/Vite folders, React Query, Redux |
| 05 | [module-layout](.ai/context/05-module-layout.md) | Domain/Application/Infrastructure layout |
| 06 | [backend-rules](.ai/context/06-backend-rules.md) | Naming, MediatR, SharedKernel, Foundation |
| 07 | [database](.ai/context/07-database.md) | SQL Server, EF, migrations, RowVersion |
| 08 | [api](.ai/context/08-api.md) | Envelope JSON, controllers, HTTP codes |
| 09 | [security](.ai/context/09-security.md) | JWT, refresh, RBAC, secrets |
| 10 | [event-driven](.ai/context/10-event-driven.md) | Domain/integration events, RabbitMQ |
| 11 | [inventory](.ai/context/11-inventory.md) | Stock, reservation 15min, concurrency |
| 12 | [roadmap](.ai/context/12-roadmap.md) | Phase 1–4, thứ tự triển khai, Docker |
| 13 | [testing-git](.ai/context/13-testing-git.md) | Tests, branches, commits |
| 14 | [ai-instructions](.ai/context/14-ai-instructions.md) | Checklist feature/module mới |
| 15 | [sources](.ai/context/15-sources.md) | `structure.txt`, `Funtion.txt` |
| 16 | [admin-portal-phases](.ai/context/16-admin-portal-phases.md) | **Admin back-office**: products, categories, orders, KM, quà, báo cáo |
| 17 | [ai-prompts](.ai/context/17-ai-prompts.md) | **Prompt chuẩn** copy-paste cho từng task / phase |

**Routing chi tiết:** [.ai/README.md](.ai/README.md)

---

## Module context (Phase 1–2)

| Module | File |
|--------|------|
| Auth | [modules/auth.md](.ai/context/modules/auth.md) |
| Products (+ Categories) | [modules/products.md](.ai/context/modules/products.md) |
| Cart | [modules/cart.md](.ai/context/modules/cart.md) |
| Orders | [modules/orders.md](.ai/context/modules/orders.md) |
| Inventory | [modules/inventory.md](.ai/context/modules/inventory.md) |
| Payments | [modules/payments.md](.ai/context/modules/payments.md) |
| Admin (reports, shell) | [modules/admin.md](.ai/context/modules/admin.md) |
| Promotions & Gifts | [modules/promotions.md](.ai/context/modules/promotions.md) |

---

## Prompt ngắn

[17-ai-prompts.md](.ai/context/17-ai-prompts.md) — copy **BASE** + 1 dòng TASK.

```
ZIONShop: .NET8 + React/Vite + SQL Server. Đọc CLAUDE.md + các file Read bên dưới. Một task/session. Domain→App→API→FE. Không PostgreSQL, không cross-DbContext, Categories trong Products.

BASE. Đọc: 12-roadmap, 16-admin §7, 17-ai-prompts. Chọn 1 task kế tiếp (Phase 1 gaps → Admin A0 → Inventory), implement, liệt kê việc còn lại. Không commit.
```

---

## Source documents

| File | Role |
|------|------|
| [structure.txt](structure.txt) | Original architecture rules |
| [Funtion.txt](Funtion.txt) | Original structure & phases |

Khi mâu thuẫn: **`.ai/context/*` + file này** là chuẩn.

*Last updated: 2026-05-18*
