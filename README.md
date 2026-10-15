# ZIONShop

Ecommerce enterprise platform: modular monolith backend (.NET 8, PostgreSQL) in this repo (`ZION_BE`), and a Next.js 16 frontend in the **separate sibling repo `ZION_FE`** (own `.git`).

This scaffold follows [CLAUDE.md](CLAUDE.md) for architecture rules and conventions.

## Layout

```text
ZION_BE/ (this repo)
|-- ZIONShop.slnx
|-- Directory.Build.props
|-- Directory.Packages.props
|-- global.json
|-- .ai/                 # agent context
|-- deploy/
|   |-- docker-compose.yml
|   |-- .env.example
|   `-- nginx/nginx.conf
|-- docs/
|   `-- DEPLOYMENT_GUIDE.md
|-- src/
|   |-- Api/ZIONShop.Api/
|   |-- Worker/ZIONShop.Worker/
|   |-- BuildingBlocks/
|   `-- Modules/
`-- tests/
    |-- Unit/
    |-- Integration/
    `-- Architecture/

../ZION_FE/               # actual frontend — separate repo, Next.js 16 + antd 5
```

## Prerequisites

- .NET SDK 10.x (build; see `global.json`, `rollForward: latestFeature`) + .NET 8 runtime (target framework)
- Node 20+
- Docker Desktop

## Run With Docker

```powershell
cp deploy/.env.example deploy/.env
docker compose -f deploy/docker-compose.yml up -d postgres redis rabbitmq seq
docker compose -f deploy/docker-compose.yml up -d --build api worker
```

- API: <http://localhost:8080/swagger>
- Swagger via nginx: <http://localhost/swagger/>
- Seq logs: <http://localhost:8081>
- RabbitMQ UI: <http://localhost:15672>
- Aspire Dashboard (traces/metrics/logs, opt-in via `OTEL_EXPORTER_OTLP_ENDPOINT`): <http://localhost:18888> — see [docs/observability.md](docs/observability.md)

Seed account: `admin@zionshop.local` / `Admin@123`.

In this compose topology, `api` sets `BackgroundProcessing__Enabled=false` and `worker` (a separate
container, same image family, built from `src/Worker/ZIONShop.Worker/Dockerfile`) owns every
consumer/outbox/background job (RabbitMQ consumer, outbox publishers, reservation-expiry/stale-coupon
scans, idempotency-key cleanup) instead. The API still owns EF Core migrations/seeding; the worker has
no controllers and only serves `/health/live` + `/health/ready`.

## Run Backend Locally

```powershell
dotnet build ZIONShop.slnx
dotnet run --project src\Api\ZIONShop.Api\ZIONShop.Api.csproj
```

Open <http://localhost:8080/swagger>.

### Run API + Worker as two separate processes

By default, running the API alone (as above) still runs every background job itself — behaviour is
unchanged from before the Worker existed. To split them into two processes instead (closer to the
Docker Compose topology):

```powershell
# Terminal 1 -- API only serves HTTP; hands background jobs to the Worker
$env:BackgroundProcessing__Enabled = "false"
dotnet run --project src\Api\ZIONShop.Api\ZIONShop.Api.csproj

# Terminal 2 -- Worker owns every consumer/outbox/background job
dotnet run --project src\Worker\ZIONShop.Worker\ZIONShop.Worker.csproj
```

The Worker needs the same `ConnectionStrings:DefaultConnection`/`EventBus:*`/`Redis:*`/`Email:*`
settings as the API (via its own `appsettings.json`, user-secrets, or environment variables) but does
not need `Jwt:*` (no authenticated HTTP endpoints) and never runs migrations itself.

## Run Frontend Locally

Frontend is the separate repo `ZION_FE` (Next.js 16). See `ZION_FE/README.md` for setup; point it at this API via `NEXT_PUBLIC_API_URL`.

## Email (SMTP)

`appsettings.json` ships with `Email:Host` empty, which makes `AuthInfrastructureExtensions` register `LoggingEmailSender` — OTP/verification codes are written to the log instead of sent. To send real email via SMTP, set `Email:Host`/`Email:UserName`/`Email:Password` (and optionally `Email:Port`, `Email:UseSsl`, `Email:FromAddress`, `Email:FromName`) through user-secrets or environment variables — never commit real credentials into `appsettings*.json`.

User secrets (local dev):

```powershell
cd src\Api\ZIONShop.Api
dotnet user-secrets set "Email:Host" "smtp.gmail.com"
dotnet user-secrets set "Email:UserName" "you@example.com"
dotnet user-secrets set "Email:Password" "<app-password>"
```

Environment variables (e.g. Docker/CI):

```text
Email__Host=smtp.gmail.com
Email__UserName=you@example.com
Email__Password=<app-password>
```

## Testing

```powershell
dotnet test ZIONShop.slnx
```
