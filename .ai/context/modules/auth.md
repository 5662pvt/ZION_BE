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

## FE module

`frontend/src/modules/auth/` — login, register pages; store tokens via Redux + `apiClient` interceptor.

## Docs to load

`03-backend-structure`, `05-module-layout`, `06-backend-rules`, `08-api`, `09-security`
