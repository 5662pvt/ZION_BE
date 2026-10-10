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
| `Promotion` | Campaign: name, type, value, date range, active, UsageLimit/PerUserLimit/UsedCount (atomic, xmin-guarded) |
| `CouponCode` | Mã coupon thuộc promotion (unique, uppercase); own UsedCount (xmin-guarded) |
| `CouponRedemption` | Reserved/Committed/Released ledger row per OrderId (unique); drives usage-limit/per-user-limit counts |
| `PromotionTarget` | not implemented — out of W7 scope |
| `GiftRule` | not implemented — out of W7 scope |

### Promotion types (enum) — implemented

- `PercentageOff`
- `FixedAmountOff`

Not yet implemented: `FreeShipping`, `GiftWithPurchase`/`GiftRule`.

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

**Gifts (A5) not implemented** — `GiftRule`, `PromotionTarget`, `FreeShipping`/`GiftWithPurchase` types, and the gift-rules API/admin screens are still pending; implement per the original sketch above when that phase starts.
