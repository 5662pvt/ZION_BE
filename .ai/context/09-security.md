# 09 — Security

## Required (MUST)

- JWT access token (short-lived) + refresh token (secure storage, rotation)
- Password hashing (`PasswordHasher` or BCrypt)
- RBAC roles: `Customer`, `Admin`, `Staff`
- Rate limiting on auth and public endpoints
- FluentValidation + model binding
- EF Core parameterized queries
- CORS explicit for frontend origin

## Rate limiting policies

Fixed-window limiters registered in `Program.cs` (`AddRateLimiter`):

| Policy | Scope | Partition key | Default limit (config key) |
| --- | --- | --- | --- |
| `auth` | login/register/forgot-password/etc. | global per window | `RateLimiting:AuthPermitLimit` (20/min) |
| `checkout` | `OrdersController.Place` only | authenticated user id, fallback IP | `RateLimiting:CheckoutPermitLimit` (10/min) |
| `search` | public product list/search GET only | client IP | `RateLimiting:SearchPermitLimit` (60/min) |

A rejected request gets `429` with the standard `ApiResponse` fail envelope (`errors[0].code = "RateLimit.Exceeded"`) and a `Retry-After` header (also exposed via CORS, alongside `Idempotent-Replayed`).

## Security headers

`SecurityHeadersMiddleware` (`src/Api/ZIONShop.Api/Middleware/`) sets `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, and `Content-Security-Policy` on every response except `/swagger*` and the `/uploads` static path (Swagger UI needs inline scripts/styles; the FE loads product images cross-origin). `UseHsts()` + `UseHttpsRedirection()` are Production-only.

## Health checks

`/health/live` (no dependency checks) and `/health/ready` (tag `ready`: `postgres`, `redis`, `rabbitmq`) are implemented in `src/Api/ZIONShop.Api/Health/`. Redis failures degrade (never fail) readiness since caching is optional. The response body never includes connection strings or exception detail. `GET /api/v1/admin/system/health` exposes the same report to Admin-only callers inside the `ApiResponse` envelope.

## Forbidden (MUST NOT)

- Plain-text passwords
- Secrets / connection strings / JWT keys in source
- Hardcoded signing keys

Use User Secrets, env vars, or gitignored `appsettings.Development.json`.

## Auth module scope

Login, register, refresh, revoke — see [modules/auth.md](modules/auth.md).

## Related docs

- Foundation JWT setup: [06-backend-rules.md](06-backend-rules.md)
- API 401/403: [08-api.md](08-api.md)
