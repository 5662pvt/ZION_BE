# Module — Products (Phase 1)

## Bounded context

Products, SKUs, **Categories**, product images, brands. Search engine = Phase 4.

## Categories

**Inside this module** — not a separate module:

- `Products.Domain/Entities/Category.cs`
- `Products.Application/Features/Categories/`

## Key entities (suggested)

- `Product`, `Category`, `ProductImage`, `Brand`, `Sku` (value object or entity)

## Features (minimum Phase 1)

- `CreateProduct`, `UpdateProduct`, `GetProduct`, `SearchProducts`
- `CreateCategory`, `GetCategories`, `GetCategoryTree`

## DbContext

`ProductsDbContext` — schema `products`

## FE module (ZION_FE)

`app/(shop)/products/` — listing, detail, category nav; `services/products.service.ts`, `hooks/useProducts.ts`.

## Docs to load

`05-module-layout`, `06-backend-rules`, `07-database`, `08-api`
