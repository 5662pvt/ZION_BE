# Module — Cart (Phase 1)

## Bounded context

Guest cart (session/cookie ID) and authenticated user cart; merge on login.

## Key entities (suggested)

- `Cart`, `CartItem` — reference `ProductId` / `SkuId` by ID only (no FK to Products DbContext)

## Features (minimum Phase 1)

- `GetCart`, `AddCartItem`, `UpdateCartItemQuantity`, `RemoveCartItem`, `ClearCart`
- `MergeGuestCart` (on login — event or command from Auth/Users)

## Rules

- Prices resolved at checkout from Products/Orders — cart stores snapshot or live price per product policy (clarify with team if unclear)
- No cross-module DbContext — fetch product validity via application service contract or cache, not direct Products DB

## FE module

`frontend/src/modules/cart/`

## Docs to load

`03-backend-structure`, `05-module-layout`, `06-backend-rules`, `08-api`
