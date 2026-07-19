# ZIONShop Deployment Guide

Guide nay di theo huong hien tai cua du an:

- Backend: .NET 8, Docker, PostgreSQL.
- Frontend: React/Vite.
- Database cloud: PostgreSQL managed service, vi du Neon, Supabase, Render PostgreSQL.
- CI/CD: GitHub Actions.
- Root repo giu gon: `backend/`, `frontend/`, `.gitignore`, `CLAUDE.md`.

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
cd backend
cp .env.example .env
docker compose up -d postgres redis rabbitmq seq
dotnet restore
dotnet run --project src\Api\ZIONShop.Api\ZIONShop.Api.csproj
```

Frontend chay rieng:

```powershell
cd frontend
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
PostgreSQL server/project
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

Deploy backend theo huong:

1. Build Docker image tu `backend/src/Api/ZIONShop.Api/Dockerfile`.
2. Push image len container registry.
3. Deploy image vao Azure Container Apps.
4. Set environment variables cho API.
5. API ket noi PostgreSQL staging/prod bang secret.

## 9. Frontend Deployment Target

Khuyen dung:

```text
Azure Static Web Apps
```

Ly do:

- Hop React/Vite.
- Co CI/CD voi GitHub.
- Serve static nhanh.
- Tach rieng frontend khoi backend.

Frontend can biet API base URL theo moi truong:

```text
staging frontend -> staging API
production frontend -> production API
```

## 10. Docker Compose Usage

File `backend/docker-compose.yml` phu hop cho local/dev stack.

Chay tu thu muc `backend`:

```powershell
cd backend
docker compose up -d postgres redis rabbitmq seq
docker compose up -d --build api
docker compose --profile dev up -d frontend-dev
```

Luu y:

- `frontend-dev` mount tu `../frontend`.
- Neu deploy production bang Azure, khong nhat thiet phai dung Docker Compose tren server.
- Docker Compose chu yeu de chay local full stack.

## 11. Database Migration Strategy

Hien tai API co the chay EF Core migrations khi startup trong Development.

Khuyen nghi:

- Local/dev: co the auto migrate.
- Staging: co the auto migrate neu team nho.
- Production: nen chay migration nhu mot buoc rieng trong pipeline, truoc khi deploy API moi.

Thu tu production an toan:

```text
backup DB -> run migrations -> deploy API -> smoke test
```

## 12. Contributor Access

Voi nguoi ngoai/team mo rong:

- Khuyen khich chay DB local bang Docker.
- Khong cap production DB.
- Neu can cloud DB, cap `zionshop-dev` hoac staging voi user rieng.
- Thu hoi quyen khi khong con tham gia.

Voi core team:

- Co the cap `zionshop-dev`.
- Staging chi dung cho kiem thu.
- Production chi pipeline va maintainer duoc dung.

## 13. Setup Checklist

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

## 14. Recommended Policy

Quy tac ngan gon:

- Code public, secrets private.
- Local dev dung Docker DB.
- Shared cloud DB chi dung cho dev/staging.
- Production DB khong public.
- Merge vao `main` deploy staging.
- Production deploy can approval.
