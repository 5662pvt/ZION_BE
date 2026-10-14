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
| `PromotionGiftItem` | Owned child collection of `Promotion` (table `promotion_gift_items`, PK (PromotionId, ProductId)) — 1-5 distinct `(ProductId, Quantity 1-10)` reward lines for `Type = Gift` |
| `GiftRedemption` | Reserved/Committed/Released ledger row per (PromotionId, OrderId) unique pair; several can exist per OrderId (one per qualifying gift promotion) |

`TargetProductIds`/`TargetCategoryIds` are `Guid[]` columns on `Promotion` (Postgres `uuid[]`, mapped directly — chosen over a child `PromotionTarget` table since targets are always read/written as a whole with their promotion, never queried independently). `Scope` gates which is used: `Order` ignores both (whole-order subtotal eligible), `Products` requires `TargetProductIds` non-empty, `Categories` requires `TargetCategoryIds` non-empty. Category membership is resolved per distinct `ProductId` in the cart via `IProductCatalogReader.GetAsync` (no Products DbContext access — see Rules).

### Promotion types (enum) — implemented

- `PercentageOff`
- `FixedAmountOff`
- `FreeShipping` (W8) — `Value`/`MaxDiscountAmount` forced to `0`/`null` regardless of request; discount = the order's shipping fee, `DiscountTarget` = `"Shipping"`. Already-free orders (shipping fee already 0) return `Promotions.CouponAlreadyFreeShipping` instead of a 0 discount.
- `Gift` (W9) — auto-applied at order placement (no coupon), stacks with a coupon. `Value`/`MaxDiscountAmount` forced to `0`/`null`; has no coupon codes (`CreateCoupon`/`GenerateCoupons`/`PromotionEvaluator` all reject it with `Promotions.CouponNotAllowedForGift`). New `Promotion.MinQuantity int?` + `GiftItems` (1-5 `PromotionGiftItem`). Conditions (all that are configured must hold): `MinOrderSubtotal` always; `Scope != Order` requires `MinQuantity >= 1` and sums `Quantity` over eligible lines (reusing `IsLineEligible`); `Scope == Order` requires `MinQuantity` null and `MinOrderSubtotal > 0`. Several qualifying gift promotions on one order → all granted. Out-of-stock gift → order still placed, that gift dropped (Orders/Inventory side).

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
`PromotionsController` is wired for Scope/targets (and, as of W9, MinQuantity/GiftItems) — `PromotionUpsertRequest`/`CreatePromotionCommand`/`UpdatePromotionCommand` all carry them through.

**Gifts (W9) implemented** — `PromotionType.Gift`, `Promotion.MinQuantity`/`GiftItems` (owned `PromotionGiftItem`, migration `PromotionGifts` hand-authored same as prior migrations, no `dotnet-ef` tooling available in the implementing session — but applied and exercised successfully against real Postgres by the full integration suite, so the DDL itself is verified; still worth a `dotnet ef migrations add`/`has-pending-model-changes` pass to confirm the Designer snapshot matches byte-for-byte before a real deploy), `GiftRedemption` aggregate + `IGiftRedemptionRepository`/`GiftRedemptionRepository`, `GiftPromotionEvaluator : IGiftPromotionEvaluator` (Infrastructure — same xmin optimistic-concurrency retry ×3 pattern as `PromotionEvaluator`, but Quote/Reserve/Release operate per-promotion since several Gift promotions can grant on one order; `ReserveGiftsAsync` is idempotent per (promotionId, orderId)). `CreateCoupon`/`GenerateCoupons` reject `Type == Gift` with `Promotions.CouponNotAllowedForGift` ("Khuyến mãi quà tặng không dùng mã giảm giá"); `PromotionEvaluator.ValidateAsync` rejects it too (defensive — unreachable in practice since a Gift promotion can never have a coupon). New query `GetGiftPreviewQuery` (`GiftsController` → `GET /api/v1/promotions/gifts/preview`, authenticated customer) quotes gifts for the caller's current cart via `ICartReader` + `IGiftPromotionEvaluator.QuoteGiftsAsync`, resolves `available` via `IStockAvailabilityReader` (contract implemented by the Inventory module) checked against gift qty + qty already in cart, and names/images via `IProductPriceLookup.GetManyAsync`. `OrderConfirmedIntegrationEventHandler` now also commits all Reserved gift redemptions of the order to Committed; `OrderCancelledIntegrationEventHandler` and the new `InventoryReservationFailedIntegrationEventHandler` both also call `ReleaseGiftsAsync`; `StaleReservationReleaseProcessor` also releases stale (>30 min) Reserved gift redemptions. Inventory reservation splitting (paid vs. gift quantity, dropping out-of-stock gift lines via `Order.DropGiftLines` + `IGiftPromotionEvaluator.ReleaseGiftAsync`) lives in the Orders/Inventory modules (ORD-GIFT track), not here.
