# 09 — Security

## Required (MUST)

- JWT access token (short-lived) + refresh token (secure storage, rotation)
- Password hashing (`PasswordHasher` or BCrypt)
- RBAC roles: `Customer`, `Admin`, `Staff`
- Rate limiting on auth and public endpoints
- FluentValidation + model binding
- EF Core parameterized queries
- CORS explicit for frontend origin

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
