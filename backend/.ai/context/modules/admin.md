# Module — Admin (Portal & Reports)

## Bounded context

**Back-office operations UI** và **read-only analytics** — **không** sở hữu Product/Order/Promotion aggregates.

| Thuộc Admin | Không thuộc Admin |
|-------------|-------------------|
| Dashboard, báo cáo tổng hợp | CRUD Product → **Products** |
| Export CSV (sau) | CRUD Category → **Products** |
| Audit log (sau) | Order lifecycle → **Orders** |
| BFF aggregate (optional) | Coupon / Gift rules → **Promotions** |

## Backend paths

```
Modules/Admin/
├── ZIONShop.Admin.Domain/          # Report read models, optional AuditEntry
├── ZIONShop.Admin.Application/     # Queries only (MVP)
├── ZIONShop.Admin.Infrastructure/  # Dapper or EF read DbContext / views
└── ZIONShop.Admin.Tests/
```

**Host API:** `Controllers/AdminController.cs` — prefix `api/v1/admin`

## Authorization (MUST)

| Policy | Roles | Dùng cho |
|--------|-------|----------|
| `AdminOnly` | Admin | Settings, staff, adjust stock |
| `StaffOrAdmin` | Staff, Admin | Orders, catalog write |

Catalog write hiện dùng `AdminOnly` trên Products/Categories — có thể mở `Staff` sau.

## Features (theo phase — xem `16-admin-portal-phases.md`)

### Phase A7 (MVP reports)

- `GetDashboardSummary` — cards: revenue today, orders pending, low stock count
- `GetRevenueReport` — `from`, `to`, `groupBy` (day|week|month)
- `GetOrdersReport` — counts by status
- `GetTopProductsReport` — top N by quantity/revenue

**Implementation note:** Có thể query cross-schema bằng **read-only SQL views** trong Admin.Infrastructure — **không** inject Orders DbContext vào Products.

## Frontend paths

```
frontend/src/modules/admin/
├── layouts/AdminLayout.tsx
├── components/AdminSidebar.tsx
├── pages/
│   ├── DashboardPage.tsx
│   ├── catalog/          # products, categories
│   ├── orders/
│   ├── marketing/        # promotions, gifts
│   ├── inventory/
│   └── reports/
├── hooks/
├── services/
└── types/
```

Routes: `/admin/*` — lazy load; guard `AdminRoute`.

## Seed / test account

- `admin@zionshop.local` / `Admin@123` (Development seed)

## Docs to load

| Task | Files |
|------|-------|
| Admin shell | `16-admin-portal-phases.md` A0, `04-frontend-structure.md`, `09-security.md` |
| Reports | `16-admin-portal-phases.md` A7, `07-database.md`, this file |
| Catalog admin UI | `modules/products.md`, `16-admin-portal-phases.md` A1–A2 |

## Anti-patterns (MUST NOT)

- `AdminDbContext` chứa bảng `products` duplicate
- Gọi `ProductsDbContext` từ Admin.Application
- Business rules (giá, tồn, coupon) trong Admin handlers
