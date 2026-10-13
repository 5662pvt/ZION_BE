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
├── ZIONShop.Admin.Domain/          # AdminErrors (Exceptions/) — no entities, no DbContext
├── ZIONShop.Admin.Application/     # Queries only — consumes ZIONShop.Contracts readers
│   ├── Common/                     # ReportRange (VN-day/month bucketing), KpiMath, CsvWriter/CsvFile
│   ├── DTOs/                       # ReportDtos.cs — exact shape of the JSON contract
│   └── Features/
│       ├── GetDashboard/
│       ├── GetTopProducts/
│       ├── GetTopCategories/
│       ├── GetPromotionPerformance/
│       ├── ExportRevenueCsv/
│       ├── ExportTopProductsCsv/
│       └── ExportPromotionsCsv/
└── ZIONShop.Admin.Infrastructure/  # still a no-op DI extension — Admin owns no persistence
```

**Host API:** `Controllers/AdminReportsController.cs` — class-level `[Route("api/v1/admin/reports")]` + `[Authorize(Policy = Policies.AdminOnly)]` (every action is admin-only).

**Data access:** Admin never queries another module's `DbContext`. All aggregation reads come from `ZIONShop.Contracts` reader interfaces (`IOrderReportReader`, `IUserStatsReader`, `IInventoryStatsReader`, `IPromotionLookup`, `ICategoryLookup`, `IProductCatalogReader`), each implemented by its owning module's Infrastructure and resolved via DI. Aggregation itself happens in memory inside the Admin query handlers (pure, deterministic, unit-testable with Moq fakes of the readers).

**Date ranges:** `from`/`to` are Vietnam-local calendar dates (`yyyy-MM-dd`, inclusive); `ReportRange.Create` converts them to a UTC `[fromUtc, toUtc)` window, computes the matching "previous period" (same length immediately before `from`), and picks day vs. month bucket granularity (day when the range is ≤92 days). Default range when both are omitted: last 30 days including today, in VN-local time (`Asia/Ho_Chi_Minh`, falling back to `SE Asia Standard Time` or a fixed +07:00 offset).

## Authorization (MUST)

| Policy | Roles | Dùng cho |
|--------|-------|----------|
| `AdminOnly` | Admin | Settings, staff, adjust stock |
| `StaffOrAdmin` | Staff, Admin | Orders, catalog write |

Catalog write hiện dùng `AdminOnly` trên Products/Categories — có thể mở `Staff` sau.

## Features (Phase A7 — reports, implemented)

- `GetDashboardQuery` → `GET /dashboard` — KPIs (revenue, completedOrders, averageOrderValue, newCustomers, placedOrders, cancelledOrders, discountTotal, awaitingShipment, lowStockProducts incl. `changePercent` vs. previous period), `revenueSeries` (zero-filled buckets), `statusBreakdown`, `paymentMethods`.
- `GetTopProductsQuery` → `GET /top-products?limit=&sortBy=revenue|quantity` — non-gift lines only; `limit` 1..50 (default 10) validated by FluentValidation, not clamped.
- `GetTopCategoriesQuery` → `GET /top-categories?sortBy=` — maps product → category via `IProductCatalogReader` + `ICategoryLookup`; uncategorized → "Chưa phân loại".
- `GetPromotionPerformanceQuery` → `GET /promotions` — coupons via `IPromotionLookup.ResolveCouponCodesAsync` (unresolved code → name=code, type="Unknown"); gifts via `GetByIdsAsync`.
- `ExportRevenueCsv/ExportTopProductsCsv/ExportPromotionsCsv` queries → `GET /export/*.csv` — `Result<CsvFile>`; UTF-8 BOM + CRLF + RFC4180 escaping via `CsvWriter`, money as plain integers via `CsvWriter.FormatMoney`.

Revenue = Subtotal − DiscountAmount + ShippingFee, attributed to the order's `Completed` status-history timestamp (not `CreatedDate`).

## Frontend paths (ZION_FE)

```
app/admin/
├── layout.tsx            # AdminLayout + sidebar
├── page.tsx               # Dashboard
├── products/              # catalog: products, categories
├── orders/
├── promotions/            # marketing: promotions, gifts
├── inventory/
└── reports/
services/admin*.service.ts
hooks/useAdmin*.ts
```

Routes: `/admin/*` — guarded by role check against `constants/roles.ts`.

## Seed / test account

- `admin@zionshop.local` / `Admin@123` (Development seed)

## Docs to load

| Task | Files |
|------|-------|
| Admin shell | `16-admin-portal-phases.md` A0, `04-frontend-structure.md`, `09-security.md` |
| Reports | `16-admin-portal-phases.md` A7, `07-database.md`, this file |
| Catalog admin UI | `modules/products.md`, `16-admin-portal-phases.md` A1–A2 |

## Tests

- Unit: `tests/Unit/ZIONShop.Admin.Tests/` — `Common/ReportRangeTests.cs` (default range, VN-day bucketing across UTC midnight, month granularity + zero-fill, invalid format/order/range-too-large), `Common/CsvWriterTests.cs` (BOM, escaping, CRLF), `Features/*HandlerTests.cs` (Moq fakes of the Contracts readers — no DbContext/EF InMemory needed since Admin has no persistence), `Features/ReportQueryValidatorTests.cs`.
- Integration: `tests/Integration/ZIONShop.Api.IntegrationTests/Reports/AdminReportsTests.cs` (HTTP-level: admin 200, customer 403, anonymous 401, invalid range 400, top-products contains the seeded product, CSV export starts with BOM) alongside the pre-existing `ReportReadersTests.cs` (exercises the raw `Contracts` readers via DI).

## Anti-patterns (MUST NOT)

- `AdminDbContext` chứa bảng `products` duplicate — Admin has no DbContext at all for reports; it only consumes `ZIONShop.Contracts` reader interfaces.
- Gọi `ProductsDbContext`/`OrdersDbContext`/... từ Admin.Application
- Business rules (giá, tồn, coupon) trong Admin handlers — Admin only aggregates data already computed by the owning module
