# ZIONShop Agent Entry Point

Read this file first. Detailed agent context now lives in `backend/.ai/context/`.

## Project

ZIONShop is an ecommerce platform with:

- Backend: .NET 8 modular monolith, Clean Architecture, CQRS, MediatR, EF Core, PostgreSQL (Npgsql; Neon for cloud). Build with .NET SDK 10.x (`global.json`, `rollForward: latestFeature`); runtime/target remains .NET 8.
- Frontend: Next.js 16 App Router + TypeScript + antd 5 + TanStack Query — separate sibling repo `../ZION_FE` (this repo's own `frontend/` folder is an empty, unused placeholder).
- Infrastructure: Docker Compose and nginx under `backend/`.

## Root Layout

```text
ZIONShop/ (this repo = ZION_BE)
|-- backend/        # real code: ZIONShop.slnx, src/, tests/
|-- frontend/        # empty placeholder — NOT used
|-- .gitignore
`-- CLAUDE.md

../ZION_FE/          # actual frontend — separate repo, Next.js 16 + antd 5
```

## Rules

- Use PostgreSQL only. Do not introduce SQL Server.
- Keep module data isolated. Do not access another module's `DbContext` directly.
- Keep Categories inside the Products module.
- Follow the dependency direction: API -> Application -> Domain <- Infrastructure.
- Use `Result<T>`, `CancellationToken`, MediatR, and FluentValidation in backend features.
- API responses use `{ success, message, data, errors, pagination }`.
- For feature work, prefer this order: Domain, Application, API, tests, Frontend.

## Context Routing

- Overview: `backend/.ai/context/01-overview.md`
- Architecture: `backend/.ai/context/02-architecture.md`
- Backend structure: `backend/.ai/context/03-backend-structure.md`
- Frontend structure: `backend/.ai/context/04-frontend-structure.md`
- Module layout: `backend/.ai/context/05-module-layout.md`
- Backend rules: `backend/.ai/context/06-backend-rules.md`
- Database: `backend/.ai/context/07-database.md`
- API: `backend/.ai/context/08-api.md`
- Security: `backend/.ai/context/09-security.md`
- Events: `backend/.ai/context/10-event-driven.md`
- Roadmap: `backend/.ai/context/12-roadmap.md`
- Testing and Git: `backend/.ai/context/13-testing-git.md`
- Task prompts: `backend/.ai/context/17-ai-prompts.md`

## Common Commands

```powershell
cd backend
dotnet build
dotnet test
docker compose up -d --build api
```

```powershell
cd ../ZION_FE
npm run lint
npm run build
```
