# 13 — Testing & Git

## Testing (MUST)

| Type | Scope |
|------|-------|
| Unit | Domain, handlers, validators |
| Integration | API + SQL Server (Testcontainers or local) |
| Concurrency | Inventory reservation, RowVersion conflicts |
| API E2E | Auth, checkout critical paths |

## Test naming

```
{MethodName}_Should_{ExpectedBehavior}_When_{State}
```

Example: `CreateOrder_Should_ReturnError_When_StockUnavailable`

## Git branches

- `feature/{module-name}-{short-description}`
- `bugfix/{issue-name}`
- `hotfix/{issue-name}`

## Commits (Conventional Commits)

```
feat: implement product search
fix: resolve inventory concurrency issue
refactor: extract order pricing service
chore: update docker compose
test: add cart merge integration tests
```
