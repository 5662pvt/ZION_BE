# ZIONShop

Ecommerce enterprise platform: modular monolith backend (.NET 8, PostgreSQL) in this repo (`ZION_BE`), and a Next.js 16 frontend in the **separate sibling repo `ZION_FE`** (own `.git`).

This scaffold follows [CLAUDE.md](../CLAUDE.md) for architecture rules and conventions.

## Layout

```text
ZION_BE/ (this repo)
|-- backend/
|   |-- ZIONShop.slnx
|   |-- Directory.Build.props
|   |-- Directory.Packages.props
|   |-- docker-compose.yml
|   |-- .env.example
|   |-- infra/
|   |   `-- nginx/nginx.conf
|   |-- src/
|   |   |-- Api/ZIONShop.Api/
|   |   |-- BuildingBlocks/
|   |   `-- Modules/
|   `-- tests/Unit/
|-- frontend/            # empty placeholder — NOT used
`-- CLAUDE.md

../ZION_FE/               # actual frontend — separate repo, Next.js 16 + antd 5
```

## Prerequisites

- .NET SDK 10.x (build; see `global.json`, `rollForward: latestFeature`) + .NET 8 runtime (target framework)
- Node 20+
- Docker Desktop

## Run With Docker

```powershell
cd backend
cp .env.example .env
docker compose up -d postgres redis rabbitmq seq
docker compose up -d --build api
```

- API: <http://localhost:8080/swagger>
- Swagger via nginx: <http://localhost/swagger/>
- Seq logs: <http://localhost:8081>
- RabbitMQ UI: <http://localhost:15672>

Seed account: `admin@zionshop.local` / `Admin@123`.

## Run Backend Locally

```powershell
cd backend
dotnet build
dotnet run --project src\Api\ZIONShop.Api\ZIONShop.Api.csproj
```

Open <http://localhost:8080/swagger>.

## Run Frontend Locally

Frontend is the separate repo `ZION_FE` (Next.js 16). See `ZION_FE/README.md` for setup; point it at this API via `NEXT_PUBLIC_API_URL`.

## Testing

```powershell
cd backend
dotnet test
```
