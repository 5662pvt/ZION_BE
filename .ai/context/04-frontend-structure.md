# 04 — Frontend Structure & Rules

> Frontend lives in the **separate sibling repo `ZION_FE`** (own `.git`), not under this repo's `frontend/` (empty/unused placeholder). Always Glob-verify paths before editing — see `ZION_FE/AGENTS.md` for FE-specific warnings (this Next.js version has breaking changes; read `node_modules/next/dist/docs/` before writing FE code).

## Stack

- **Next.js 16** (App Router) + TypeScript
- **antd 5** for UI components
- **TanStack Query** for server state
- No Redux, no Vite, no React Router — this is Next.js App Router, not the old Vite SPA layout.

## Folder tree

```
ZION_FE/
├── app/
│   ├── (shop)/           # storefront routes
│   ├── (auth)/           # login/register routes
│   ├── admin/            # /admin/* — back-office
│   └── providers.tsx     # TanStack QueryClientProvider, antd ConfigProvider
├── services/
│   ├── apiClient.ts       # base client + interceptors
│   └── <domain>.service.ts
├── hooks/
│   └── use<Domain>.ts     # TanStack Query hooks per domain
├── types/
└── constants/
    └── roles.ts           # role constants — never hardcode role strings
```

## Rules (MUST)

| Required | Forbidden |
|----------|-----------|
| TanStack Query for **server state** | API calls in large page components |
| `services/<domain>.service.ts` for all API calls | Duplicate fetch logic / scattered API URLs |
| `hooks/use<Domain>.ts` wrapping TanStack Query | Business logic in components |
| Role checks via `constants/roles.ts` | Hardcoded permissions/roles |
| antd components for UI | Massive single-file pages |

## Feature pattern

```
services/products.service.ts   # API calls
hooks/useProducts.ts            # TanStack Query hooks
app/(shop)/products/            # pages/components
```

## API integration

- Base URL from env (`NEXT_PUBLIC_API_URL`)
- Expect backend envelope from [08-api.md](08-api.md): `{ success, message, data, errors, pagination }`
- Map errors to UI consistently in `services/apiClient.ts`

## Related docs

- API contract: [08-api.md](08-api.md)
- Security (tokens): [09-security.md](09-security.md)
- Admin portal phases/paths: [16-admin-portal-phases.md](16-admin-portal-phases.md)
