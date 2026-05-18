# ZIONShop

Ecommerce Enterprise Platform — Modular Monolith (.NET 8) + React (Vite + TS).

This scaffold follows [CLAUDE.md](./CLAUDE.md). See that file for architecture rules and conventions; this README is just for getting up and running.

## Layout

```
ZIONShop/
├── backend/
│   ├── ZIONShop.slnx                 # solution (SDK 10 default format)
│   ├── Directory.Build.props
│   ├── Directory.Packages.props      # central package management
│   ├── src/
│   │   ├── BuildingBlocks/           # SharedKernel, Common, Auth, EventBus, Caching, Logging, Contracts
│   │   ├── Modules/                  # Auth, Users, Products (+ Categories), Cart [full]
│   │   │                             # Orders, Inventory, Payments, Promotions, Notifications, Reviews, Admin [skeleton]
│   │   └── Api/ZIONShop.Api/         # Web API host (controllers, Program.cs, Swagger)
│   └── tests/Unit/                   # xUnit (Products tests as sample)
├── frontend/                         # React + Vite + TS demo (auth + products + cart)
├── infra/
│   └── nginx/nginx.conf
├── docker-compose.yml                # api, sqlserver, redis, rabbitmq, seq, nginx
├── .env.example
└── CLAUDE.md
```

## Prerequisites

- **.NET 8 SDK + runtime** (the codebase targets `net8.0`). On a machine that only has .NET 10, `dotnet build` works (reference assemblies are downloaded via NuGet), but `dotnet run`/`dotnet test` requires the .NET 8 runtime — install it from <https://dotnet.microsoft.com/download/dotnet/8.0> or use the Docker stack below.
- Node 20+ (for the frontend)
- Docker Desktop (for the local stack)

## Run locally with Docker (recommended)

```powershell
cp .env.example .env
docker compose up -d sqlserver redis rabbitmq seq
docker compose up -d --build api
docker compose --profile dev up -d frontend-dev
```

- API: <http://localhost:8080/swagger>
- Swagger via nginx (after `docker compose up nginx`): <http://localhost/swagger/>
- Seq logs: <http://localhost:8081>
- RabbitMQ UI: <http://localhost:15672> (guest/guest)
- Frontend dev: <http://localhost:5173>

The Api container runs EF Core migrations on startup (Development env only) and seeds an admin user.

**Seed account:** `admin@zionshop.local` / `Admin@123`.

## Run locally without Docker

```powershell
# 1. Spin up SQL Server (Docker or local install)
docker compose up -d sqlserver

# 2. Build + run the API
cd backend
dotnet build
dotnet run --project src\Api\ZIONShop.Api\ZIONShop.Api.csproj
```

Open <http://localhost:8080/swagger>.

## Run frontend dev server

```powershell
cd frontend
npm install
npm run dev
```

Visits <http://localhost:5173>. The dev server proxies `/api/*` to <http://localhost:8080>.

## Endpoints (Phase 1)

| Module | Endpoint | Auth |
|---|---|---|
| Auth | POST `/api/v1/auth/register`, `/login`, `/refresh`; GET `/me` | mixed |
| Users | GET/PUT `/api/v1/users/me`; GET/POST `/api/v1/users/me/addresses` | Bearer |
| Products | GET `/api/v1/products`, `/{id}`, `/by-slug/{slug}`; POST/PUT (Admin) | mixed |
| Categories | GET `/api/v1/categories`; POST (Admin) | mixed |
| Cart | GET/DELETE `/api/v1/cart`; POST/PUT/DELETE `/api/v1/cart/items[/{id}]` | guest OK |

All responses use the `ApiResponse<T>` envelope (`{ success, message, data, errors, pagination }`).

## Module status

- **Full vertical slice:** Auth, Users, Products (+ Categories), Cart — Domain + Application (CQRS) + Infrastructure (EF Core SQL Server + LINQ) wired into `ZIONShop.Api`.
- **Skeleton only:** Orders, Inventory, Payments, Promotions, Notifications, Reviews, Admin — csproj structure + no-op DI extension; see each module's `README.md` and implement following the Auth/Products template.

## Testing

```powershell
cd backend
dotnet test
```

Sample tests live in `tests/Unit/ZIONShop.Products.Tests`. Naming follows `MethodName_Should_ExpectedBehavior_When_State` (CLAUDE.md §14.1).
