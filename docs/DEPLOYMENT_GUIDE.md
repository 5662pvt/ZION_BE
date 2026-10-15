# ZIONShop Deployment Guide

Guide nay di theo huong hien tai cua du an:

- Backend: .NET 8, Docker, PostgreSQL. (Repo nay la `ZION_BE`.) Build voi .NET SDK 10.x (`global.json`, `rollForward: latestFeature`); runtime/target van la .NET 8.
- Frontend: Next.js 16 (App Router) + antd 5 — repo rieng `ZION_FE` (sibling repo, khong phai `ZION_BE/frontend` — thu muc do rong/khong dung).
- Database cloud: PostgreSQL managed service, vi du Neon, Supabase, Render PostgreSQL.
- CI/CD: GitHub Actions.
- Root repo giu gon: `src/`, `tests/`, `deploy/`, `docs/`, `.ai/`, `.gitignore`, `CLAUDE.md`.

## 1. Muc tieu moi truong

Nen chia thanh 3 moi truong rieng:

```text
local      - Moi dev tu chay tren may rieng
staging    - Moi truong test sau khi merge code
production - Moi truong that cho nguoi dung
```

Khong cho dev ket noi truc tiep vao production DB.

## 2. Local Development

Mac dinh moi nguoi nen chay DB local bang Docker. Cach nay an toan nhat cho repo public.

```powershell
cp deploy/.env.example deploy/.env
docker compose -f deploy/docker-compose.yml up -d postgres redis rabbitmq seq
dotnet restore ZIONShop.slnx
dotnet run --project src\Api\ZIONShop.Api\ZIONShop.Api.csproj
```

Frontend chay rieng (repo `ZION_FE`):

```powershell
cd ../ZION_FE
npm install
npm run dev
```

Quy tac:

- Moi dev co DB local rieng.
- Khong can xin connection string cloud de code feature thong thuong.
- Khong commit file `.env`.

## 3. PostgreSQL Database Plan

Tao PostgreSQL database theo cau truc:

```text
PostgreSQL instance/project
|-- zionshop-dev
|-- zionshop-staging
`-- zionshop-prod
```

Y nghia:

- `zionshop-dev`: team core co the dung chung khi can test cloud.
- `zionshop-staging`: CI/CD deploy tu branch `main`.
- `zionshop-prod`: production, chi deployment pipeline duoc dung.

Khong dung chung `zionshop-prod` cho viec code hang ngay.

## 4. PostgreSQL Setup

Lam theo thu tu:

1. Tao account tren Neon, Supabase, Render hoac managed PostgreSQL provider khac.
2. Tao PostgreSQL project/server, vi du `zionshop-postgres`.
3. Tao 3 database: `zionshop-dev`, `zionshop-staging`, `zionshop-prod`.
4. Tao PostgreSQL user rieng cho moi moi truong:
   - `zionshop_dev_user`
   - `zionshop_staging_user`
   - `zionshop_prod_user`
5. Cap quyen toi thieu can thiet cho tung user.
6. Bat firewall/IP allowlist chi cho IP can thiet.
7. Luu connection string vao secret manager, khong dua vao GitHub source.

Vi du connection string local trong `.env` rieng cua moi dev:

```env
ConnectionStrings__DefaultConnection=Host=<host>;Port=5432;Database=zionshop-dev;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true
```

## 5. Public Code, Private Secrets

Repo co the public, nhung secrets phai private.

Duoc public:

- Source code.
- Dockerfile.
- Docker Compose template.
- `.env.example`.
- README/deployment guide.

Khong duoc public:

- DB password.
- JWT signing key production.
- SMTP password.
- Azure publish profile/token.
- Production connection string.

## 6. GitHub Repository Flow

Quy trinh code nen dung:

```text
feature branch -> Pull Request -> CI checks -> merge main -> deploy staging -> approve release -> deploy production
```

Branch rules de xuat:

- Khong push truc tiep vao `main`.
- Moi thay doi vao `main` phai qua Pull Request.
- PR phai pass backend test va frontend build.
- Production deploy can manual approval.

## 7. GitHub Secrets

Trong GitHub repo, tao secrets cho CI/CD.

Secrets cho staging:

```text
AZURE_CREDENTIALS_STAGING
POSTGRES_CONNECTION_STRING_STAGING
JWT_SIGNING_KEY_STAGING
```

Secrets cho production:

```text
AZURE_CREDENTIALS_PRODUCTION
POSTGRES_CONNECTION_STRING_PRODUCTION
JWT_SIGNING_KEY_PRODUCTION
```

Neu dung email SMTP:

```text
EMAIL_HOST
EMAIL_PORT
EMAIL_USERNAME
EMAIL_PASSWORD
EMAIL_FROM_ADDRESS
EMAIL_FROM_NAME
```

## 8. Backend Deployment Target

Khuyen dung:

```text
Azure Container Apps
```

Ly do:

- Hop voi backend Docker.
- De cau hinh environment variables.
- De scale sau nay.
- Phu hop voi CI/CD tu GitHub Actions.

Backend gom 2 container rieng, build tu cung repo:

- `src/Api/ZIONShop.Api/Dockerfile` — API: controllers, EF Core migrations/seed, chi phuc vu HTTP khi `BackgroundProcessing:Enabled=false`.
- `src/Worker/ZIONShop.Worker/Dockerfile` — Worker: RabbitMQ consumer, outbox publisher, cac background job (reservation-expiry, stale-coupon release, idempotency-key cleanup); khong co controller, chi co `/health/live` + `/health/ready`.

Deploy backend theo huong:

1. Build 2 Docker image (API + Worker) tu 2 Dockerfile tren (build context deu la repo root).
2. Push ca 2 image len container registry.
3. Deploy ca 2 image vao Azure Container Apps (2 app rieng, cung mang/VNet).
4. Set environment variables cho API (them `BackgroundProcessing__Enabled=false`) va Worker (cung `ConnectionStrings`/`EventBus`/`Redis`/`Email`, khong can `Jwt:*`).
5. API/Worker ket noi PostgreSQL staging/prod bang secret.

## 9. Frontend Deployment Target

Khuyen dung:

```text
Azure Static Web Apps
```

Ly do:

- Phu hop Next.js (repo `ZION_FE`).
- Co CI/CD voi GitHub.
- Serve static nhanh.
- Tach rieng frontend khoi backend.

Frontend can biet API base URL theo moi truong:

```text
staging frontend -> staging API
production frontend -> production API
```

## 10. Docker Compose Usage

File `deploy/docker-compose.yml` phu hop cho local/dev stack (postgres, redis, rabbitmq, seq, api, worker, nginx).

```powershell
docker compose -f deploy/docker-compose.yml up -d postgres redis rabbitmq seq
docker compose -f deploy/docker-compose.yml up -d --build api worker
```

Luu y:

- `api` chay voi `BackgroundProcessing__Enabled=false`; `worker` (service rieng, cung build tu repo nay qua `src/Worker/ZIONShop.Worker/Dockerfile`) moi la noi RabbitMQ consumer/outbox publisher/cac background job thuc su chay. Chi mot noi chay nhung job nay tai 1 thoi diem trong moi truong binh thuong — khong bat ca hai cung luc voi `BackgroundProcessing:Enabled=true`.
- Nginx trong compose nay chi la reverse proxy cho API (`/api/`, `/swagger/`, `/healthz`); frontend (`ZION_FE`) deploy rieng, khong nam trong compose nay.
- Neu deploy production bang Azure, khong nhat thiet phai dung Docker Compose tren server.
- Docker Compose chu yeu de chay local full stack backend.
- Observability (OpenTelemetry traces/metrics/logs qua Aspire Dashboard, opt-in): xem [observability.md](observability.md).

## 11. Health Check Endpoints

API (va Worker, cung check) co 3 endpoint health, ngoai `/healthz` tren API (giu nguyen, tuong thich nguoc):

```text
GET /health/live                    - luon 200 khi process con song, khong check dependency
GET /health/ready                   - 200 (Healthy/Degraded) hoac 503 (Unhealthy)
GET /api/v1/admin/system/health     - can JWT Admin, bao boc trong ApiResponse envelope, check nhieu hon /health/ready
```

`/health/ready` check 3 dependency (tag `ready`): `postgres` (SELECT 1, timeout 3s, Unhealthy neu loi), `redis` (PING, timeout 2s; **Degraded** neu khong ket noi duoc -- Redis la optional, app fail open; Healthy "not configured" neu khong dang ky), `rabbitmq` (chi check khi `EventBus:Provider=RabbitMq`; Healthy "in-memory provider" khi dung InMemory).

`GET /api/v1/admin/system/health` chay **toan bo** health check da dang ky (khong loc theo tag `ready`), nen ngoai 3 dependency tren con co them `worker` (`WorkerHealthCheck`): Healthy `"consumers=N"` neu queue chinh dang co consumer, Unhealthy `"no consumer: start the worker"` neu khong ai tieu thu queue. `worker` khong mang tag `ready` nen **khong** anh huong `/health/ready` -- quen chay Worker la van de vận hành can admin biet, khong phai ly do de container/LB coi instance API la khong san sang.

Response JSON khong bao gio chua connection string hay exception stack trace -- chi `{ status, totalDurationMs, checks: [{ name, status, durationMs, description }] }`.

Dung cho load balancer / container orchestrator probe:

```text
liveness probe  -> /health/live
readiness probe -> /health/ready
```

## 12. Database Migration Strategy

Hien tai API co the chay EF Core migrations khi startup trong Development.

Khuyen nghi:

- Local/dev: co the auto migrate.
- Staging: co the auto migrate neu team nho.
- Production: nen chay migration nhu mot buoc rieng trong pipeline, truoc khi deploy API moi.

Thu tu production an toan:

```text
backup DB -> run migrations -> deploy API -> smoke test
```

## 13. Contributor Access

Voi nguoi ngoai/team mo rong:

- Khuyen khich chay DB local bang Docker.
- Khong cap production DB.
- Neu can cloud DB, cap `zionshop-dev` hoac staging voi user rieng.
- Thu hoi quyen khi khong con tham gia.

Voi core team:

- Co the cap `zionshop-dev`.
- Staging chi dung cho kiem thu.
- Production chi pipeline va maintainer duoc dung.

## 14. Setup Checklist

Lam theo thu tu:

1. Dua code len GitHub public repo.
2. Bao ve branch `main`.
3. Tao Azure account.
4. Tao PostgreSQL managed database/server.
5. Tao DB: `zionshop-dev`, `zionshop-staging`, `zionshop-prod`.
6. Tao Azure Container Apps cho API staging/prod.
7. Tao Azure Static Web Apps cho frontend staging/prod.
8. Tao GitHub Actions workflow cho PR:
   - `dotnet restore`
   - `dotnet test`
   - `npm install`
   - `npm run build`
9. Tao GitHub Actions workflow deploy staging khi merge `main`.
10. Tao GitHub Actions workflow deploy production khi release/manual approval.
11. Luu secrets vao GitHub Environments.
12. Test staging end-to-end.
13. Chay migration production.
14. Deploy production.

## 15. Recommended Policy

Quy tac ngan gon:

- Code public, secrets private.
- Local dev dung Docker DB.
- Shared cloud DB chi dung cho dev/staging.
- Production DB khong public.
- Merge vao `main` deploy staging.
- Production deploy can approval.
