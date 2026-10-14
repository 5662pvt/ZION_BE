# ZIONShop Agent Entry Point

Read this file first. Detailed agent context now lives in `.ai/context/`.

## Project

ZIONShop is an ecommerce platform with:

- Backend: .NET 8 modular monolith, Clean Architecture, CQRS, MediatR, EF Core, PostgreSQL (Npgsql; Neon for cloud). Build with .NET SDK 10.x (`global.json`, `rollForward: latestFeature`); runtime/target remains .NET 8.
- Frontend: Next.js 16 App Router + TypeScript + antd 5 + TanStack Query — separate sibling repo `../ZION_FE`.
- Infrastructure: Docker Compose and nginx under `deploy/`.

## Root Layout

```text
ZION_BE/ (this repo)
|-- .ai/            # agent context
|-- deploy/         # docker-compose.yml, .env.example, nginx/
|-- docs/           # DEPLOYMENT_GUIDE.md
|-- src/            # Api/, BuildingBlocks/, Modules/
|-- tests/          # Unit/, Integration/, Architecture/
|-- ZIONShop.slnx, global.json, Directory.Build.props, Directory.Packages.props
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

- Overview: `.ai/context/01-overview.md`
- Architecture: `.ai/context/02-architecture.md`
- Backend structure: `.ai/context/03-backend-structure.md`
- Frontend structure: `.ai/context/04-frontend-structure.md`
- Module layout: `.ai/context/05-module-layout.md`
- Backend rules: `.ai/context/06-backend-rules.md`
- Database: `.ai/context/07-database.md`
- API: `.ai/context/08-api.md`
- Security: `.ai/context/09-security.md`
- Events: `.ai/context/10-event-driven.md`
- Roadmap: `.ai/context/12-roadmap.md`
- Testing and Git: `.ai/context/13-testing-git.md`
- Task prompts: `.ai/context/17-ai-prompts.md`

## Common Commands

```powershell
dotnet build ZIONShop.slnx
dotnet test ZIONShop.slnx
docker compose -f deploy/docker-compose.yml up -d --build api
```

```powershell
cd ../ZION_FE
npm run lint
npm run build
```
