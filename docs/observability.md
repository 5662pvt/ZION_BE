# Observability (OpenTelemetry + Aspire Dashboard)

Tai lieu nay mo ta nhung gi API xuat ra qua OpenTelemetry (OTLP), cach chay dashboard local de xem, va cach doc dau vet (trace) cua mot request xuyen qua outbox/RabbitMQ.

> API chi xuat OTLP khi bien moi truong `OTEL_EXPORTER_OTLP_ENDPOINT` duoc set. Khong set -> khong co overhead, khong gui di dau. Day la thiet ke mac dinh "tat", dev tu bat khi can.

## 1. Du lieu duoc thu thap

Service name: `zionshop-api` cho API, `zionshop-worker` cho Worker (tu `OTEL_SERVICE_NAME`; moi host co default rieng neu bien nay khong duoc set — xem `ZIONShop.Observability.AddZionObservability`). Ca 2 host dung chung pipeline (`ZIONShop.Observability`), nen cung bo metric/trace nhu nhau duoi day; chi khac service name va host nao dang chay consumer/outbox/background job tai thoi diem do (xem `BackgroundProcessing:Enabled` trong README.md).

### Spans (traces)

- ASP.NET Core (incoming HTTP request, tru `/health*`, `/healthz`, `/swagger*` va static `/uploads*`), HttpClient (outgoing call), Npgsql (SQL query toi PostgreSQL).
- `ZIONShop.Messaging` (ActivitySource rieng cho outbox/RabbitMQ):
  - `"{Event} publish"` — span khi outbox publish mot integration event.
  - `"{Event} process"` — span khi consumer xu ly event nhan duoc (RabbitMQ; voi EventBus InMemory, khong co span "process" rieng, span con cho handler nam truc tiep duoi span publish).
  - Span con cho moi handler chay trong qua trinh process.
- `ZIONShop.Caching` khong phat span — chi phat metrics (xem phan Metrics ben duoi).

### Metrics

- `ZIONShop.Messaging`:
  - `messaging.published`, `messaging.consumed`, `messaging.handler.failed`, `messaging.handler.retried`, `messaging.handler.dead_lettered` — counter.
  - `messaging.handler.duration` — histogram thoi gian xu ly handler.
  - `outbox.pending`, `outbox.failed` — gauge, theo tung module.
  - `rabbitmq.queue.messages` — gauge, tag phan biet queue chinh (`main`) va dead-letter queue (`dlq`).
- `ZIONShop.Caching`: `cache.hits`, `cache.misses`, `cache.errors` — counter.
- Runtime metrics (.NET): GC, thread pool, exception count...
- ASP.NET Core / HttpClient / Npgsql instrumentation metrics di kem (http.server.duration, http.client.duration...).

### Logs

Serilog ghi keo theo cac field sau de noi voi trace:

```text
TraceId, SpanId, CorrelationId, MessageId, EventType, OrderId
```

`TraceId`/`SpanId` cho phep nhay tu mot dong log sang dung trace tren dashboard; `CorrelationId`/`MessageId`/`EventType`/`OrderId` cho phep loc theo nghiep vu (vi du: tat ca log lien quan 1 order).

## 2. Chay local

```powershell
docker compose -f deploy/docker-compose.yml up -d postgres redis rabbitmq aspire-dashboard
```

Chay API voi OTLP endpoint tro ve dashboard (chay API ngoai Docker, tro vao container qua port map `4317:18889`):

PowerShell:

```powershell
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
$env:EventBus__Provider = "RabbitMq"
dotnet run --project src\Api\ZIONShop.Api\ZIONShop.Api.csproj
```

Bash:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 EventBus__Provider=RabbitMq dotnet run --project src/Api/ZIONShop.Api/ZIONShop.Api.csproj
```

Mo dashboard: <http://localhost:18888>

(Neu chay API trong cung docker-compose nhu service `api`, compose da set `OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889` san — khong can set tay.)

## 3. Theo doi 1 order xuyen qua he thong

1. Goi `POST /api/v1/orders` (Swagger hoac client). Lay `OrderId`/`CorrelationId` tu response hoac log.
2. Vao Aspire Dashboard -> tab **Traces** -> tim trace cua request vua goi (filter theo thoi gian, hoac theo attribute `OrderId`/`CorrelationId` neu da gan vao span).
3. Trong trace, span HTTP request ban dau se la root. Span outbox publish (`"OrderCreated publish"` chang han) **co the xuat hien tre vai giay** — outbox la poll-based (background job doc bang `outbox.pending`), khong publish dong bo trong request.
4. Theo trace tiep sang cac span `"{Event} process"` cua module consumer (Inventory, Payments...), va span con cua tung handler ben trong.
5. Span SQL (Npgsql) nam long trong tung buoc — doi chieu voi query cham neu can.
6. Tab **Logs**: loc theo `TraceId` cua trace dang xem de thay dung cac dong log lien quan (bao gom field `OrderId`, `EventType`, `MessageId`, `CorrelationId`).

## 4. Metric can theo doi — gia tri bat thuong nghia la gi

| Metric | Binh thuong | Bat thuong / nghia la gi |
| --- | --- | --- |
| `outbox.pending` | Thap, dao dong ngan | Tang lien tuc khong giam -> publisher/outbox poll bi nghen hoac RabbitMQ down |
| `outbox.failed` | 0 | > 0 -> co event khong publish duoc sau retry, can xem log loi |
| `rabbitmq.queue.messages` (tag `main`) | Thap, gan 0 | Tang dan -> consumer xu ly cham hon publish (backlog) |
| `rabbitmq.queue.messages` (tag `dlq`) | 0 | > 0 -> co message loi sau het retry, roi vao dead-letter, can xu ly tay |
| `messaging.handler.duration` (p95) | On dinh theo baseline | Tang dot ngot -> handler/dependency (DB, call ngoai) cham |
| `cache.hits` / `cache.misses` | Hit ratio cao cho data hay doc | Hit ratio giam manh -> cache bi invalidate qua nhieu hoac Redis khong on dinh |
| `http.server.duration` (p95, theo route) | On dinh | Tang dot ngot tren 1 route cu the -> regression hoac dependency cham |

## 5. Lab debug: outbox khi RabbitMQ down

1. Dung RabbitMQ: `docker compose -f deploy/docker-compose.yml stop rabbitmq`.
2. Dat 1 order qua API. Request van tra 200 (outbox ghi vao DB truoc, publish la buoc sau, bat dong bo).
3. Quan sat metric `outbox.pending` tren dashboard — se tang len va khong giam vi khong publish duoc.
4. Bat lai RabbitMQ: `docker compose -f deploy/docker-compose.yml start rabbitmq`.
5. Quan sat `outbox.pending` giam dan ve 0 khi outbox poll publish lai thanh cong cac message con lai.
6. De tao mot "poison message" (message luon loi khi xu ly) de xem DLQ gauge (`rabbitmq.queue.messages` tag `dlq`) tang, can mot handler co loi that/that dedicated test — khong co san cong khai de "co tinh gay loi" ngoai viec can thiep code; ghi nhan day la buoc thu cong khi can kiem thu.

## 6. Cong cu admin cho outbox / inbox / DLQ / worker

Thay vi chi xem metric/log, trang "Hệ thống" cua admin goi thang `api/v1/admin/system` (`AdminSystemController`, yeu cau `Policies.AdminOnly`) de tra cuu va can thiep:

- `GET /modules` — danh sach module co outbox / co inbox.
- `GET /outbox?module=&status=&page=&pageSize=`, `GET /outbox/{module}/{id}` — liet ke / xem chi tiet mot outbox message (status suy ra tu `Attempts`/`ProcessedOn`, giong cach `outbox.pending`/`outbox.failed` tinh). Payload trong response chi tiet da duoc mask cac field nhay cam (email, phone, recipientName, streetAddress, address, password, token, secret, card -> `"***"`).
- `POST /outbox/{module}/{id}/retry`, `POST /outbox/retry-failed?module=` — reset `Attempts`/`Error` cua (cac) row dang Failed de outbox poll publish lai, khong can cho het `MaxAttempts` hoac can thiep DB tay.
- `GET /dlq?limit=`, `POST /dlq/requeue`, `DELETE /dlq/{messageId}` — xem/day lai/xoa message trong DLQ qua API thay vi phai vao RabbitMQ management UI thao tac tay. Peek khong xoa message (chi set lai "redelivered" va co the doi thu tu trong queue); broker khong ket noi duoc tra ve 503.
- `GET /health` (admin) co them check `worker`: Healthy `"consumers=N"` neu queue chinh co >= 1 consumer dang chay, Unhealthy `"no consumer: start the worker"` neu 0 — phat hien ngay truong hop quen chay Worker (hoac API voi `BackgroundProcessing:Enabled=false`) ma khong ai tieu thu queue, thay vi phai doi `rabbitmq.queue.messages` tang len moi biet. Check nay **khong** co tag `ready` nen **khong** anh huong `/health/ready` (readiness probe cua container/LB): khong ai chay Worker la mot tinh trang vận hành can canh bao, khong phai loi cua tung instance API — API van phai bao Healthy/200 de LB khong rut het instance ra khoi pool.

Chi tiet thiet ke (store abstraction theo module, cach mask payload, cach peek/requeue/delete DLQ) o `.ai/context/10-event-driven.md`.

## 7. Luu y production

- Khong dung `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true` ngoai moi truong local. Production/staging phai bat auth cho dashboard (hoac dung backend OTLP khac co auth, vi du collector rieng).
- Dung sampling de giam chi phi/luong du lieu: cau hinh `Otel:SamplingRatio` (ty le span duoc giu lai) phu hop voi tai production, khong de 100% mac dinh tren moi truong tai cao.
- Khong bao gio dua secret (password, token, connection string, PII nhay cam) vao span attribute hoac log field — chi dua ID/metadata nghiep vu (OrderId, EventType...).
