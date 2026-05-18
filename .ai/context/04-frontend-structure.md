# 04 — Frontend Structure & Rules

## Folder tree

```
frontend/
├── package.json
├── vite.config.ts
└── src/
    ├── app/              # providers: QueryClient, Redux, Router
    ├── shared/
    │   ├── components/
    │   ├── utils/
    │   ├── types/
    │   └── constants/
    ├── modules/          # mirror backend domains
    │   ├── auth/
    │   ├── products/
    │   ├── cart/
    │   ├── orders/
    │   ├── checkout/
    │   ├── account/
    │   └── admin/
    ├── services/         # apiClient + interceptors
    ├── hooks/
    ├── layouts/
    ├── routes/           # lazy imports
    └── store/            # Redux — global UI/auth only
```

## Rules (MUST)

| Required | Forbidden |
|----------|-----------|
| React Query for **server state** | API calls in large page components |
| Redux Toolkit for **global client state** | Business logic in components |
| `React.lazy` for routes | Massive single-file pages |
| Centralized `apiClient` + auth interceptor | Hardcoded permissions |
| Permission-based routes | Duplicate fetch logic |
| Reusable UI in `shared/components` | Scattered API URLs |

## Feature module pattern

```
modules/products/
├── pages/
├── components/
├── hooks/           # useProducts, useProductDetail
├── services/        # productsApi.ts
└── types/
```

## API integration

- Base URL from env (`VITE_API_URL`)
- Expect backend envelope from [08-api.md](08-api.md): `{ success, message, data, errors, pagination }`
- Map errors to UI consistently in `apiClient` interceptor

## Related docs

- API contract: [08-api.md](08-api.md)
- Security (tokens): [09-security.md](09-security.md)
