# Module — Reviews

## Bounded context

Đánh giá sản phẩm sau khi mua (rating 1-5, title/content, moderation admin).

## Backend paths

```
Modules/Reviews/
├── ZIONShop.Reviews.Domain/
├── ZIONShop.Reviews.Application/
└── ZIONShop.Reviews.Infrastructure/
```

`ReviewsDbContext` — schema `reviews`

## Key entities

| Entity | Mô tả |
|--------|--------|
| `Review` | ProductId, UserId, OrderId (snapshot từ `IPurchaseVerifier`), Rating 1-5, Title?, Content, AuthorName (snapshot DisplayName hoặc email mask "ng***"), ProductName (snapshot), Status (Published/Hidden), HiddenReason?. Unique (UserId, ProductId). |
| `ProductRatingSummary` | ProductId (PK), Count, Sum, Star1..Star5 — recomputed từ đầu trên mọi create/update/delete/hide/show, trong cùng `SaveChangesAsync` với review, xmin-guarded (retry ×3 qua `ProductRatingSummaryUpdater`). |

## Features

- `CreateReview` — yêu cầu `IPurchaseVerifier.GetCompletedOrderIdAsync` trả về order (else Forbidden), chưa review sản phẩm này (else Conflict 409), sản phẩm phải `Published` (qua `IProductCatalogReader`).
- `UpdateReview` / `DeleteReview` — owner only (else NotFound), không tự unhide khi edit.
- `GetProductReviews` — public, chỉ Published, filter theo rating, paged mới nhất trước.
- `GetReviewSummary` / `GetReviewSummaries` (batch ≤100) — đọc trực tiếp `ProductRatingSummary`, thiếu → 0/0.
- `GetReviewEligibility` — trả `myReview` kể cả khi Hidden (owner vẫn sửa được).
- Admin: `AdminSearchReviews` (status/rating/keyword — keyword case-insensitive UPPER+LIKE+escape trên ProductName/AuthorName/Content), `HideReview` / `ShowReview`.

## Integration

Phụ thuộc `ZIONShop.Contracts`: `IPurchaseVerifier` (Orders), `IUserContactReader` (Auth), `IProductCatalogReader` (Products). Không reference DbContext của module khác.

## API

```
GET    /api/v1/products/{productId}/reviews                [anonymous]
GET    /api/v1/products/{productId}/reviews/summary         [anonymous]
GET    /api/v1/reviews/summaries?productIds=a,b,c            [anonymous, max 100]
GET    /api/v1/products/{productId}/reviews/eligibility     [Authorize]
POST   /api/v1/products/{productId}/reviews                 [Authorize]
PUT    /api/v1/reviews/{id}                                  [owner]
DELETE /api/v1/reviews/{id}                                  [owner]
GET    /api/v1/admin/reviews                                 [StaffOrAdmin]
POST   /api/v1/admin/reviews/{id}/hide                       [StaffOrAdmin]
POST   /api/v1/admin/reviews/{id}/show                       [StaffOrAdmin]
```

## Rules (MUST)

- Không reference Products/Orders/Auth DbContext trực tiếp — chỉ qua Contracts interfaces + snapshot fields (`ProductName`, `AuthorName`, `OrderId`).
- `Result<T>` cho mọi lỗi nghiệp vụ; không `throw`.
- `ProductRatingSummary` luôn được tính lại từ đầu (không incremental) để tránh drift.

## Current status

**Implemented (W8)** — CRUD review storefront + admin moderation, rating summary (single + batch), eligibility check. Migration hand-authored nếu `dotnet-ef` không chạy được trong session — verify bằng `dotnet ef migrations add` trước khi deploy thật.
