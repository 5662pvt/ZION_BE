# Backend Implementation Status

**Project:** ZIONShop Backend  
**Report date:** 2026-10-07  
**Purpose:** Track exactly what is done, what is in progress, and what is not started yet.

---

## Status Meaning

| Status | Meaning |
|---|---|
| DONE | Code exists for the required scope and can be treated as completed for this phase, except normal testing/hardening. |
| IN PROGRESS | Real code exists, but important required work is still missing. |
| TODO | Required by roadmap/context, but no meaningful implementation exists yet. |
| SKELETON | Project/folder exists only as a placeholder. |
| RISK | Something exists but should be fixed before production. |

---

## One Page Summary

| Area | Overall status | Clear conclusion |
|---|---|---|
| Foundation / BuildingBlocks | IN PROGRESS | Core building blocks exist, but production hardening and tests are not complete. |
| Auth | IN PROGRESS | Main auth features exist and focused unit tests were added. Integration/API E2E tests and security/config hardening remain. |
| Users | IN PROGRESS | Profile/address basics plus update/remove/default address exist. Integration/API tests remain. |
| Products + Categories | IN PROGRESS | Main catalog + full Categories CRUD (Create/Update/Delete/GetCategories tree) exist. Needs broader tests; no image upload API yet. |
| Cart | IN PROGRESS | Main cart features exist. Cross-module dependency on `Products.Application` removed in W0 (2026-10-07). Needs tests. |
| Orders | TODO | Not implemented. Only skeleton project exists. |
| Inventory | TODO | Not implemented. Only skeleton project exists. |
| Payments | TODO | Not implemented. Only skeleton project exists. |
| Promotions | TODO | Not implemented. Only skeleton project exists. |
| Reviews | TODO | Not implemented. Only skeleton project exists. |
| Notifications | TODO | Not implemented. Only skeleton project exists. |
| Admin backend | TODO | Not implemented. Only skeleton project exists. |
| Unit tests | IN PROGRESS | Auth (3 files), Users (1 file), Products (2 files: CreateProduct, SearchProducts) have unit tests. Cart has none. Broader Products coverage remains. |
| Integration/API tests | TODO | No integration test project found. |
| CI/CD | TODO | No `.github/workflows` found. |
| Production readiness | IN PROGRESS | Architecture is started, but app is not production-ready yet. |

---

## Roadmap Progress

| Phase | Modules | Status | What this means |
|---|---|---|---|
| Phase 1 | Auth, Users, Products, Cart | IN PROGRESS | These modules have real implementation, but are not fully finished because tests, hardening, and a few flows are missing. |
| Phase 2 | Orders, Inventory, Payments | TODO | Required for checkout. Not implemented yet. |
| Phase 3 | Promotions, Reviews, Notifications | TODO | Required for engagement/marketing. Not implemented yet. |
| Phase 4 | Admin | TODO | Required for back-office/reporting. Not implemented yet. |

---

## Phase 1 Detailed Task Status

### Auth

| Task | Status | Evidence in source | Remaining work |
|---|---|---|---|
| Register user | DONE | `Modules/Auth/.../Features/Register` exists | Add unit/integration tests. |
| Login | DONE | `Modules/Auth/.../Features/Login` exists | Add API E2E test. |
| Refresh token | DONE | `Modules/Auth/.../Features/RefreshToken` exists; unit tests verify old token revocation and new token issuance | Add API E2E test later. |
| Revoke/logout token | DONE | `Modules/Auth/.../Features/RevokeToken` exists; unit tests verify revoke and idempotent missing-token behavior | Add API E2E test later. |
| Current user `/me` | DONE | `Modules/Auth/.../Features/Me` exists | Add API test. |
| Email verification | DONE | `VerifyEmail`, `ResendVerification` exist | Add tests and email delivery config validation. |
| Forgot/reset password | DONE | `ForgotPassword`, `ResetPassword` exist; unit tests verify reset code, password hash update, email confirmation, and refresh-token revocation | Add API E2E/security review later. |
| Auth DbContext/migrations | DONE | `AuthDbContext` and migrations exist; `AuthDbSeeder` seeds `admin@zionshop.local` | Add integration tests against PostgreSQL. |
| Auth controller/API | DONE | `AuthController.cs` exists | Add API E2E tests. |
| Auth unit tests | DONE | `tests/Unit/ZIONShop.Auth.Tests` has 3 test files covering refresh token, revoke token, and reset password | Add login/register/verify-email tests later if desired. |
| Auth integration/API E2E tests | TODO | No integration/API E2E test project found | Add API E2E tests when `Microsoft.AspNetCore.Mvc.Testing` or equivalent test host is available. |
| Auth production config | DONE (2026-10-07) | JWT `SigningKey` and `appsettings.json` DB password replaced with placeholders in W0; `appsettings.Development.json` keeps local docker values (expected) | None — keep placeholders in sync if config shape changes. |

### Users

| Task | Status | Evidence in source | Remaining work |
|---|---|---|---|
| Get profile | DONE | `Modules/Users/.../Features/GetProfile` exists | Add tests. |
| Update profile | DONE | `Modules/Users/.../Features/UpdateProfile` exists | Add tests. |
| Add address | DONE | `Modules/Users/.../Features/AddAddress` exists | Add tests. |
| List addresses | DONE | `Modules/Users/.../Features/ListAddresses` exists | Add tests. |
| Update/remove/default address | DONE | `Features/UpdateAddress`, `Features/RemoveAddress`, `Features/SetDefaultAddress` exist; endpoints added to `UsersController` | Add API/integration tests later. |
| Users DbContext/migrations | DONE | `UsersDbContext` and migrations exist | Add integration tests. |
| Users controller/API | DONE | `UsersController.cs` exists | Add API tests. |
| Users unit tests | DONE | `tests/Unit/ZIONShop.Users.Tests` has 1 test file covering add/update/set-default/remove address | Add profile and integration/API tests later. |
| Users integration/API tests | TODO | No integration/API E2E test project found | Add API/integration tests later. |

### Products + Categories

| Task | Status | Evidence in source | Remaining work |
|---|---|---|---|
| Create product | DONE | `Features/CreateProduct` exists | Existing unit test found. Add API/integration test. |
| Update product | DONE | `Features/UpdateProduct` exists | Add unit/API tests. |
| Get product | DONE | `Features/GetProduct` exists | Add unit/API tests. |
| Search products | DONE | `Features/SearchProducts` exists | Existing unit test found. Add pagination/filter integration test. |
| Archive product | DONE | `Features/ArchiveProduct` exists | Add tests and verify search filter behavior. |
| Publish product | DONE | `Features/PublishProduct` exists | Add tests. |
| Categories | DONE | `Features/Categories` has Create/Update/Delete/GetCategories (tree); `CategoriesController.cs` exposes all of them | Add tests. |
| Products DbContext/migrations | DONE | `ProductsDbContext` and migrations exist; `ProductsDbSeeder` seeds sample data | Add integration tests and soft-delete/concurrency checks. |
| Products API | DONE | `ProductsController.cs` and `CategoriesController.cs` exist | Add API E2E tests. |
| Product image upload | TODO | `Product` has a `ProductImage` list (URL only) but no upload endpoint | Add upload API (e.g. `IImageStorage` + Cloudinary/local), planned for W3. |
| Products tests | IN PROGRESS | `CreateProductCommandHandlerTests.cs`, `SearchProductsQueryHandlerTests.cs` exist | Add tests for update, get, archive, publish, categories, validation. |

### Cart

| Task | Status | Evidence in source | Remaining work |
|---|---|---|---|
| Get cart | DONE | `Features/GetCart` exists | Add tests. |
| Add cart item | DONE | `Features/AddItem` exists | Add tests. |
| Update item quantity | DONE | `Features/UpdateItem` exists | Add tests. |
| Remove item | DONE | `Features/RemoveItem` exists | Add tests. |
| Clear cart | DONE | `Features/ClearCart` exists | Add tests. |
| Merge guest cart | DONE | `Features/MergeGuestCart` exists | Ensure it is called after login; add integration/API test. |
| Cart DbContext/migrations | DONE | `CartDbContext` and migrations exist | Add integration tests. |
| Cart controller/API | DONE | `CartController.cs` exists | Add API E2E tests. |
| Cart tests | TODO | No Cart test project/test files found | Add unit + integration tests. |
| Module boundary | DONE in W0 (2026-10-07) | `IProductPriceLookup` moved to `ZIONShop.Contracts/Products`; `Cart.Application` no longer references `Products.Application` directly | None — keep new contracts cross-module usages behind `ZIONShop.Contracts` going forward. |

---

## Phase 2 Detailed Task Status

### Orders

| Task | Status | Evidence in source | Remaining work |
|---|---|---|---|
| Domain model: Order, OrderLine, OrderStatus | TODO | No real implementation found | Implement Domain first. |
| Place order / create from cart | TODO | No feature folder found | Implement Application command/handler/validator. |
| Get order | TODO | No feature folder found | Implement query/API. |
| Get orders for user | TODO | No feature folder found | Implement query/API. |
| Cancel order | TODO | No feature folder found | Implement command/API. |
| Publish `OrderCreatedIntegrationEvent` | TODO | Contract exists generally, but flow is not implemented | Publish after successful commit. |
| Orders DbContext/migrations | TODO | No `OrdersDbContext` found | Add Infrastructure persistence. |
| Orders controller/API | TODO | No Orders controller found | Add API endpoint. |
| Orders DI registration | DONE | `Add<M>Application()`/`Add<M>Infrastructure()` skeleton already wired in `Program.cs` | Fill in once Domain/Application are implemented. |
| Orders tests | TODO | No Orders tests found | Add unit + integration tests. |

### Inventory

| Task | Status | Evidence in source | Remaining work |
|---|---|---|---|
| Domain model: InventoryItem, StockReservation | TODO | No real implementation found | Implement Domain first. |
| Reserve stock | TODO | No feature folder found | Implement handler with concurrency protection. |
| Release reservation | TODO | No feature folder found | Implement command/event handler. |
| Confirm reservation | TODO | No feature folder found | Implement command/event handler. |
| Adjust stock | TODO | No feature folder found | Implement admin command/API. |
| Expire reservations after 15 minutes | TODO | No background job found | Implement hosted service/job. |
| Consume `OrderCreatedIntegrationEvent` | TODO | No handler found | Add integration event handler. |
| Publish inventory events | TODO | No flow found | Publish reserved/failed events. |
| Inventory DbContext/migrations | TODO | No `InventoryDbContext` found | Add Infrastructure persistence. |
| Inventory DI registration | DONE | `Add<M>Application()`/`Add<M>Infrastructure()` skeleton already wired in `Program.cs` | Fill in once Domain/Application are implemented. |
| Inventory tests | TODO | No Inventory tests found | Add unit, integration, and concurrency tests. |

### Payments

| Task | Status | Evidence in source | Remaining work |
|---|---|---|---|
| Domain model: Payment, PaymentIntent, PaymentStatus | TODO | No real implementation found | Implement Domain first. |
| Create payment intent | TODO | No feature folder found | Implement command/API. |
| Confirm payment / webhook | TODO | No feature folder found | Implement handler with signature validation. |
| Get payment status | TODO | No feature folder found | Implement query/API. |
| Idempotency keys | TODO | No implementation found | Add idempotency model/checks. |
| Publish payment events | TODO | No flow found | Publish completed/failed events. |
| Payments DbContext/migrations | TODO | No `PaymentsDbContext` found | Add Infrastructure persistence. |
| Payments tests | TODO | No Payments tests found | Add unit + integration tests. |

---

## Phase 3 Detailed Task Status

### Promotions

| Task | Status | Evidence in source | Remaining work |
|---|---|---|---|
| Promotion domain model | TODO | Only skeleton project found | Implement Promotion, CouponCode, PromotionTarget, GiftRule. |
| Create/update promotion | TODO | No feature folder found | Implement commands/API. |
| Search/get promotion | TODO | No feature folder found | Implement queries/API. |
| Activate/deactivate promotion | TODO | No feature folder found | Implement commands/API. |
| Generate coupon codes | TODO | No feature folder found | Implement batch generation. |
| Validate coupon | TODO | No feature folder found | Implement checkout contract. |
| Gift rules | TODO | No feature folder found | Implement gift rule features. |
| Promotions DbContext/migrations | TODO | No `PromotionsDbContext` found | Add Infrastructure persistence. |
| Promotions tests | TODO | No Promotions tests found | Add unit + integration tests. |

### Reviews

| Task | Status | Evidence in source | Remaining work |
|---|---|---|---|
| Reviews domain/application/infrastructure | SKELETON | Only placeholder project files/extensions found | Define module scope and implement features. |
| Reviews API | TODO | No Reviews controller found | Add API after module design. |
| Reviews tests | TODO | No Reviews tests found | Add tests. |

### Notifications

| Task | Status | Evidence in source | Remaining work |
|---|---|---|---|
| Notification model/handlers | SKELETON | Only placeholder project files/extensions found | Implement email/SMS event handlers. |
| Consume checkout/payment events | TODO | No handlers found | Add integration event consumers. |
| Notifications API | TODO | No Notifications controller found | Add only if needed. |
| Notifications tests | TODO | No Notifications tests found | Add tests. |

---

## Phase 4 Detailed Task Status

### Admin

| Task | Status | Evidence in source | Remaining work |
|---|---|---|---|
| Admin module shell | SKELETON | Admin Domain/Application/Infrastructure projects exist | Replace placeholder with real reports/read models. |
| Dashboard summary | TODO | No feature folder found | Implement query. |
| Revenue report | TODO | No feature folder found | Implement query/read model. |
| Orders report | TODO | No feature folder found | Implement query/read model. |
| Top products report | TODO | No feature folder found | Implement query/read model. |
| Admin API | TODO | No `AdminController` found | Add `api/v1/admin` endpoints. |
| Admin tests | TODO | No Admin tests found | Add query/API tests. |

---

## Cross-Cutting Work Status

| Work item | Status | Evidence | Remaining work |
|---|---|---|---|
| API response envelope | DONE | Common API response/result mapping exists | Keep consistent across all future controllers. |
| Global exception handler | DONE | Registered in `Program.cs` | Add integration tests for error responses. |
| API versioning | DONE | `/api/v1/...` routes exist | Keep consistent for new APIs. |
| Swagger | DONE | Swagger configured | Add XML docs only if required later. |
| Rate limiting | IN PROGRESS | Auth limiter exists | Add public endpoint policies if needed. |
| Event bus base | IN PROGRESS | `EventBus` building block has both InMemory and RabbitMQ implementations; `Contracts` already defines `UserRegistered`, `OrderCreated`, `InventoryReserved`, `PaymentCompleted` events | Implement real Phase 2 checkout event chain (producers/consumers). |
| Redis caching base | IN PROGRESS | Caching building block + Redis compose service exist | Add concrete caching use cases. |
| Docker compose local infra | DONE | `api`, `postgres` (postgres:16-alpine), `redis`, `rabbitmq`, `seq`, `nginx` exist | Verify full compose flow after build/test fixes. |
| CORS | DONE in W0 (2026-10-07) | `http://localhost:3000` (ZION_FE Next.js dev) added to allowed origins | Add staging/prod FE origins when deployed. |
| Unit tests | IN PROGRESS | Auth, Users, and Products tests exist | Add Cart tests and broader Products tests. |
| Integration tests | TODO | No `tests/Integration` found | Add API + PostgreSQL (Testcontainers) integration test project. |
| CI/CD | TODO | No workflow file found | Add GitHub Actions workflow. |
| SDK pinning | DONE | `global.json` pins SDK `8.0.423`, `rollForward: latestFeature` | None. |
| Warning policy | RISK | `TreatWarningsAsErrors=false` | Enable in CI/Release if team agrees. |
| Secrets/config | DONE in W0 (2026-10-07) | `appsettings.json` DB password and JWT `SigningKey` replaced with placeholders; real values only in gitignored `appsettings.Development.json` (local docker values, expected) | Keep placeholders in sync when adding new secrets. |

---

## What Is Actually Done Now

| Done item | Scope |
|---|---|
| Backend solution/project layout | `Api`, `BuildingBlocks`, `Modules`, `tests` exist. |
| BuildingBlocks base | SharedKernel, Common, Auth, EventBus, Caching, Logging, Contracts exist. |
| Phase 1 module shells | Auth, Users, Products, Cart have Domain/Application/Infrastructure projects. |
| Phase 1 core feature folders | Auth, Users, Products, Cart have real Application feature folders. |
| Phase 1 DbContexts | Auth, Users, Products, Cart have DbContext classes and migrations. |
| Phase 1 controllers | Auth, Users, Products, Categories, Cart controllers exist. |
| Auth unit tests | Refresh token rotation, revoke/logout behavior, and reset password security behavior are covered. |
| Users unit tests | Add/update/set-default/remove address behavior is covered. |
| Products unit tests | Two Products test files exist. |
| Local infrastructure compose | PostgreSQL, Redis, RabbitMQ, Seq, API, nginx are defined in `docker-compose.yml`. |
| SDK pin | `global.json` pins .NET SDK `8.0.423` (`rollForward: latestFeature`). |

---

## What Is Being Worked On / Half Done

| In-progress item | Why it is not done yet |
|---|---|
| Auth module | Main features and focused unit tests exist, but integration/API E2E tests and production security/config validation are missing. |
| Users module | Profile/address flows including update/remove/default exist, but integration/API tests and profile tests are still missing. |
| Products module | Main catalog features exist, but category completeness and broader tests are missing. |
| Cart module | Main cart features exist; cross-module dependency on `Products.Application` was cleaned up in W0. Tests are still missing. |
| Event-driven architecture | EventBus exists, but checkout event chain is not implemented. |
| Testing | Auth, Users, and Products unit tests exist; no integration/API/concurrency test coverage. |
| Production readiness | Architecture exists, but CI, config hardening, tests, and Phase 2 checkout modules are missing. |

---

## What Has Not Started Yet

| Not-started item | Required by |
|---|---|
| Orders real implementation | Phase 2 roadmap |
| Inventory real implementation | Phase 2 roadmap |
| Payments real implementation | Phase 2 roadmap |
| Promotions real implementation | Phase 3 roadmap |
| Reviews real implementation | Phase 3 roadmap |
| Notifications real implementation | Phase 3 roadmap |
| Admin reports/read-model backend | Phase 4 roadmap |
| Integration test project | `.ai/context/13-testing-git.md` |
| API E2E tests | `.ai/context/13-testing-git.md` |
| Inventory concurrency tests | `.ai/context/07-database.md`, `.ai/context/11-inventory.md` |
| GitHub Actions CI workflow | `.ai/context/12-roadmap.md` |

---

## Next Recommended Order

| Order | Work |
|---|---|
| 1 | Add backend CI workflow: restore, build, test. |
| 2 | Add integration test project for Auth, Products, Cart (Testcontainers PostgreSQL). |
| 3 | Add missing Cart unit tests and broader Products tests for categories/update/archive/publish. |
| 4 | Add Auth/Users API E2E tests once an API test host package is available. |
| 5 | Finish Phase 1 hardening before starting broad Phase 2. |
| 6 | Implement Orders Domain/Application/API/tests. |
| 7 | Implement Inventory Domain/Application/API/tests with concurrency protection. |
| 8 | Implement Payments Domain/Application/API/tests with webhook/idempotency, then connect checkout event flow. |

See **"Kế hoạch 2026-10"** below for the fuller wave-by-wave sequencing (W0–W6) that this order feeds into.

---

## Kế hoạch 2026-10

Roadmap cụ thể hoá, thay thế thứ tự wave trong "Next Recommended Order" ở trên khi hai bảng khác nhau — bảng này mới hơn.

| Wave | Nội dung |
|---|---|
| W0 | Docs + boundary cleanup (repo này: Postgres facts, FE = ZION_FE, `Cart.Application` hết phụ thuộc trực tiếp `Products.Application`). |
| W1 | FE foundation (ZION_FE: layout, providers, apiClient, constants/roles.ts). |
| W2 | FE storefront catalog/auth/cart + BE tests (Cart, Products, Integration test project dùng Testcontainers PostgreSQL). |
| W3 | Admin A0–A2 + image upload (`IImageStorage`, Cloudinary, local fallback) + checkout contracts. |
| W4 | Orders ∥ Inventory. |
| W5 | Payments (`IPaymentGateway` + `FakePaymentGateway`; COD + fake online) ∥ FE checkout. |
| W6 | Event chain end-to-end + E2E tests + admin orders/inventory screens. |

**Backlog (sau W6):** Promotions + Gifts, Notifications, Reviews, Admin reports, CI.

**Checkout MVP (quyết định):** yêu cầu đăng nhập; thanh toán COD hoặc fake online gateway; phí ship cố định có thể cấu hình; địa chỉ lấy từ address book (Users module).

---

## Source Documents

| File |
|---|
| `.ai/context/01-overview.md` |
| `.ai/context/02-architecture.md` |
| `.ai/context/03-backend-structure.md` |
| `.ai/context/05-module-layout.md` |
| `.ai/context/06-backend-rules.md` |
| `.ai/context/07-database.md` |
| `.ai/context/08-api.md` |
| `.ai/context/09-security.md` |
| `.ai/context/10-event-driven.md` |
| `.ai/context/12-roadmap.md` |
| `.ai/context/13-testing-git.md` |
| `.ai/context/modules/*.md` |
