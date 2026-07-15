# ZIONShop

Ecommerce enterprise platform: modular monolith backend (.NET 8) and React frontend (Vite + TypeScript).

This scaffold follows [CLAUDE.md](../CLAUDE.md) for architecture rules and conventions.

## Layout

```text
ZIONShop/
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
|-- frontend/
|   `-- src/
`-- CLAUDE.md
```

## Prerequisites

- .NET 8 SDK/runtime
- Node 20+
- Docker Desktop

## Run With Docker

```powershell
cd backend
cp .env.example .env
docker compose up -d sqlserver redis rabbitmq seq
docker compose up -d --build api
docker compose --profile dev up -d frontend-dev
```

- API: <http://localhost:8080/swagger>
- Swagger via nginx: <http://localhost/swagger/>
- Seq logs: <http://localhost:8081>
- RabbitMQ UI: <http://localhost:15672>
- Frontend dev: <http://localhost:5173>

Seed account: `admin@zionshop.local` / `Admin@123`.

## Run Backend Locally

```powershell
cd backend
dotnet build
dotnet run --project src\Api\ZIONShop.Api\ZIONShop.Api.csproj
```

Open <http://localhost:8080/swagger>.

## Run Frontend Locally

```powershell
cd frontend
npm install
npm run dev
```

Open <http://localhost:5173>.

## Testing

```powershell
cd backend
dotnet test
```
