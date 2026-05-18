# 14 — AI Instructions (Claude / Cursor)

When generating ZIONShop code, **MUST**:

1. Read [CLAUDE.md](../../CLAUDE.md) index + only relevant `.ai/context/` files
2. Preserve module boundaries — no cross-module Infrastructure
3. Use naming from [06-backend-rules.md](06-backend-rules.md)
4. **SQL Server + EF Core** only
5. **Categories** inside Products module
6. Full feature slice: Command/Query + Handler + Validator + mapping
7. `Result<T>` for expected failures
8. `CancellationToken` on async methods
9. No duplicate logic — use events/contracts
10. Ask if business rules are unclear
11. Maintainability > performance > brevity
12. Production-ready code (no TODO unless asked)

## New feature checklist

- [ ] Owning module identified
- [ ] Domain entity + invariants
- [ ] Command/Query + Handler + Validator in `Features/`
- [ ] DTOs + AutoMapper
- [ ] Repository + EF configuration
- [ ] Migration (`dotnet ef migrations add`)
- [ ] Thin controller endpoint
- [ ] Unit tests
- [ ] Integration event if cross-module
- [ ] FE: `services/`, `hooks/`, `pages/` in matching module

## New module checklist

- [ ] Domain, Application, Infrastructure, Tests projects
- [ ] DI registration in host `Api`
- [ ] DbContext + SQL Server migrations
- [ ] Integration event contracts
- [ ] Add `modules/{name}.md` if boundaries are non-trivial

## Token-saving rule

**Do not load the full monolithic doc.** Use routing table in [.ai/README.md](../README.md).
