# Module — Promotions (+ Gifts)

## Bounded context

Coupons, campaigns, giảm giá theo điều kiện, **quà tặng kèm đơn** (gift rules).

**Phase platform:** 3 (engagement)  
**Admin UI phase:** A4 (promotions), A5 (gifts) — see `16-admin-portal-phases.md`

## Backend paths

```
Modules/Promotions/
├── ZIONShop.Promotions.Domain/
├── ZIONShop.Promotions.Application/
├── ZIONShop.Promotions.Infrastructure/
└── Tests/
```

`PromotionsDbContext` — schema `promotions`

## Key entities (implemented — coupons only; gifts still pending, see Current status)

| Entity | Mô tả |
|--------|--------|
| `Promotion` | Campaign: name, type, value, date range, active, UsageLimit/PerUserLimit/UsedCount (atomic, xmin-guarded), `Scope` (Order/Products/Categories) + `TargetProductIds`/`TargetCategoryIds` |
| `CouponCode` | Mã coupon thuộc promotion (unique, uppercase); own UsedCount (xmin-guarded) |
| `CouponRedemption` | Reserved/Committed/Released ledger row per OrderId (unique); drives usage-limit/per-user-limit counts |
| `GiftRule` | not implemented — out of W8 scope |

`TargetProductIds`/`TargetCategoryIds` are `Guid[]` columns on `Promotion` (Postgres `uuid[]`, mapped directly — chosen over a child `PromotionTarget` table since targets are always read/written as a whole with their promotion, never queried independently). `Scope` gates which is used: `Order` ignores both (whole-order subtotal eligible), `Products` requires `TargetProductIds` non-empty, `Categories` requires `TargetCategoryIds` non-empty. Category membership is resolved per distinct `ProductId` in the cart via `IProductCatalogReader.GetAsync` (no Products DbContext access — see Rules).

### Promotion types (enum) — implemented

- `PercentageOff`
- `FixedAmountOff`
- `FreeShipping` (W8) — `Value`/`MaxDiscountAmount` forced to `0`/`null` regardless of request; discount = the order's shipping fee, `DiscountTarget` = `"Shipping"`. Already-free orders (shipping fee already 0) return `Promotions.CouponAlreadyFreeShipping` instead of a 0 discount.

Not yet implemented: `GiftWithPurchase`/`GiftRule`.

## Features — backend (MVP)

### Promotions (A4)

- `CreatePromotion`
- `UpdatePromotion`
- `GetPromotionById`
- `SearchPromotions` (admin list)
- `ActivatePromotion` / `DeactivatePromotion`
- `GenerateCouponCodes` (batch)
- `ValidateCoupon` (storefront checkout — Phase 2/3 integration)

### Gifts (A5)

- `CreateGiftRule`
- `UpdateGiftRule`
- `ListGiftRules`
- `DeactivateGiftRule`
- `EvaluateGiftsForCart` (application service — called at checkout via contract/event)

## Integration

| Event / call | When |
|--------------|------|
| Checkout applies coupon | Orders/Payments calls `IPromotionEvaluator` (interface in Contracts) |
| Cart preview gifts | Cart module calls evaluator — **no** Promotions DbContext in Cart |

Publish `PromotionActivatedIntegrationEvent` nếu cần cache invalidation.

## API (suggested)

```
GET    /api/v1/promotions              # admin search
POST   /api/v1/promotions              # AdminOnly
GET    /api/v1/promotions/{id}
PUT    /api/v1/promotions/{id}
POST   /api/v1/promotions/{id}/coupons/generate

GET    /api/v1/gift-rules
POST   /api/v1/gift-rules
PUT    /api/v1/gift-rules/{id}
```

Hoặc nested: `/api/v1/promotions/{id}/gift-rules` — chọn một convention và giữ nhất quán.

## Admin FE screens

| Screen | Route |
|--------|-------|
| Promotion list | `/admin/promotions` |
| Promotion form | `/admin/promotions/new`, `/:id/edit` |
| Gift rules list | `/admin/gifts` |
| Gift rule form | `/admin/gifts/new`, `/:id/edit` |

## Rules (MUST)

- Không reference Products DbContext — chỉ `ProductId` + optional snapshot name for display
- `Result<T>` khi coupon invalid / expired
- Date range validation trong domain
- Usage limit atomic (row version hoặc DB constraint)

## Docs to load

`05-module-layout.md`, `06-backend-rules.md`, `07-database.md`, `08-api.md`, `10-event-driven.md`, `16-admin-portal-phases.md` (A4, A5)

## Current status

**Coupons implemented (W7)** — `Promotion` + `CouponCode` + `CouponRedemption`, `PromotionsDbContext` (schema `promotions`), `PromotionEvaluator : IPromotionEvaluator` (xmin optimistic-concurrency retry ×3 on Reserve/Release), `PromotionsController` (admin CRUD/activate/deactivate/coupons + storefront `coupons/validate`), `OrderConfirmedIntegrationEventHandler`/`OrderCancelledIntegrationEventHandler` consumers, `StaleReservationReleaseBackgroundService` (releases Reserved redemptions older than 30 min, every 5 min). Migration `InitialPromotions` hand-authored (no `dotnet-ef` tooling available in the implementing session — regenerate/verify with `dotnet ef migrations add` before relying on it for a real deploy).

**Scopes + FreeShipping implemented (W8)** — `Promotion.Scope`/`TargetProductIds`/`TargetCategoryIds` (migration `PromotionScopes`, hand-authored same as `InitialPromotions` — verify with `dotnet ef migrations has-pending-model-changes`), `PromotionType.FreeShipping`, `Promotion.IsLineEligible(productId, categoryId)` (pure domain rule), `PromotionEvaluator.ResolveEligibleBaseAsync`/`ComputeDiscountAsync` (Infrastructure — the only place that calls `IProductCatalogReader`). New error `Promotions.CouponNotApplicable` ("Mã giảm giá không áp dụng cho sản phẩm trong giỏ") when a Products/Categories-scoped coupon matches no cart line; `Promotions.CouponAlreadyFreeShipping` for a FreeShipping coupon on an order whose shipping is already free.
**Known gap — `PromotionsController` not yet wired for Scope/targets.** `CreatePromotionCommand`/`UpdatePromotionCommand` gained `Scope`/`TargetProductIds`/`TargetCategoryIds` with back-compat defaults (`"Order"`/empty) so the existing controller (outside this track's allowed files) still compiles unchanged — but it does not read those fields off `PromotionUpsertRequest` yet, so admin-created promotions are always `Order`-scoped until `PromotionsController.Create`/`Update` and `PromotionUpsertRequest` are updated to pass them through. The W8 integration tests exercise Products/Categories/FreeShipping by seeding `Promotion`/`CouponCode` directly via `PromotionsDbContext` (see `PromotionsTestHelpers.SeedScopedPromotionAsync`), not through that HTTP endpoint.

**Gifts (A5) not implemented** — `GiftRule`, `GiftWithPurchase`, and the gift-rules API/admin screens are still pending; implement per the original sketch above when that phase starts.
