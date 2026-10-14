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
docker compose -f deploy/docker-compose.yml up -d --build api
```

- API: <http://localhost:8080/swagger>
- Swagger via nginx: <http://localhost/swagger/>
- Seq logs: <http://localhost:8081>
- RabbitMQ UI: <http://localhost:15672>

Seed account: `admin@zionshop.local` / `Admin@123`.

## Run Backend Locally

```powershell
dotnet build ZIONShop.slnx
dotnet run --project src\Api\ZIONShop.Api\ZIONShop.Api.csproj
```

Open <http://localhost:8080/swagger>.

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
