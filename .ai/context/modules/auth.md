# Module — Auth (Phase 1)

## Bounded context

Login, register, JWT access token, refresh token rotation, revoke, password reset (later).

## Backend paths

`Modules/Auth/` — Domain, Application, Infrastructure, Tests

## Key entities (suggested)

- `User` (or reference Users module by ID — prefer Auth owns credentials only)
- `RefreshToken`

## Features (minimum Phase 1)

- `RegisterUser`
- `Login`
- `RefreshToken`
- `RevokeRefreshToken`

## Security (MUST read)

[09-security.md](../09-security.md)

## FE module (ZION_FE)

`app/(auth)/` — login, register pages; `services/auth.service.ts`, `hooks/useAuth.ts` (TanStack Query); tokens handled via `services/apiClient.ts` interceptor.

## Docs to load

`03-backend-structure`, `05-module-layout`, `06-backend-rules`, `08-api`, `09-security`
