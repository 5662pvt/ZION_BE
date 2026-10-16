# Baseline hiệu năng — Cache ON vs Cache OFF (W17)

> Số liệu dưới đây là số liệu **thật**, lấy trực tiếp từ các file JSON/`stdout` do orchestrator
> chạy thật ngày 2026-10-16 và lưu tại `tests/Load/results/baseline/{search,product-detail,place-order}-{cache-on,cache-off}.{json,txt}`.
> Không có số liệu nào bị suy đoán/điền tay.

## Môi trường

| Mục | Giá trị |
| --- | --- |
| Ngày chạy | 2026-10-16 |
| CPU | Intel Core i7-1260P (16 logical CPUs) |
| RAM | 31.7 GB |
| OS | Windows 11 Pro 10.0.26200 |
| Docker Desktop engine | 29.6.2 |
| `grafana/k6` image | `0.54.0` (chạy trong container, gọi `host.docker.internal:8080`) |
| Postgres / Redis / RabbitMQ | `postgres:16-alpine` / `redis:7-alpine` / `rabbitmq:3-management` |
| API chạy ở | host trực tiếp, `dotnet run`, Debug build, `ASPNETCORE_ENVIRONMENT=Development`, Serilog console logging ON, OTEL exporter OFF, EventBus RabbitMq với consumer + outbox publisher chạy trong API host (single instance) |
| Database | Postgres local, **đã có data từ các lần chạy dev trước đó** (không reset) |

## Cấu hình chạy

| Biến | Giá trị dùng khi chạy |
| --- | --- |
| `Seed__LoadTestCustomers` | `50` |
| `RateLimiting__*PermitLimit` | nâng lên `100000` (auth/checkout/search) |
| `USERS` (place-order) | `50` |
| `Caching__Enabled` | `true` (cache-on, Redis) / `false` (cache-off, `NullDistributedCacheService`) |
| API | cùng một binary, **restart giữa hai label** (cache-on → cache-off) |
| Warm-up | ~10 s idle sau khi API start, không có traffic warm-up thủ công |
| `search.js` | `constant-arrival-rate` 50 req/s × 60 s |
| `product-detail.js` | `constant-arrival-rate` 100 req/s × 60 s (80% by slug, 20% by id) |
| `place-order.js` | `constant-arrival-rate` 10 iterations/s × 60 s; mỗi iteration: `DELETE /api/v1/cart` → add 1-2 item → `POST /api/v1/orders` (`Idempotency-Key` mới); login + address một lần/VU |

## Bảng kết quả

Thời gian theo ms (trừ khi ghi rõ), throughput theo req/s (từ `http_reqs.rate`), error-rate theo %.
**p99 không có trong JSON** — k6 `summaryTrendStats` mặc định chỉ xuất `avg/min/med/max/p(90)/p(95)`, không gồm `p(99)`. Ghi "n/a"; fix ở mục "Cách tái hiện" / item 4 dưới đây để lần chạy sau có p99.

### `search.js` — GET /api/v1/products (KHÔNG cache)

| Metric | cache-on | cache-off |
| --- | --- | --- |
| p50 (med) | 9.11 ms | 7.59 ms |
| p95 | 14.52 ms | 12.68 ms |
| p99 | n/a (xem chú ý trên) | n/a |
| max | 305.23 ms | 211.05 ms |
| avg | 9.87 ms | 8.24 ms |
| throughput | 49.77 req/s | 49.85 req/s |
| error-rate | 0.00% | 0.00% |
| dropped_iterations | 0 (không xuất hiện trong metrics) | 0 |

### `product-detail.js` — GET /api/v1/products/by-slug/{slug} + by id (CÓ cache)

| Metric | cache-on | cache-off |
| --- | --- | --- |
| p50 (med) | 3.01 ms | 4.57 ms |
| p95 | 5.58 ms | 10.75 ms |
| p99 | n/a | n/a |
| max | 378.86 ms | 6587.29 ms (≈6.59 s) |
| avg | 4.53 ms | 44.44 ms |
| throughput | 99.97 req/s | 90.58 req/s |
| error-rate (`http_req_failed`) | 0.00% | 0.00% |
| dropped_iterations | 3 | 566 |

### `place-order.js` — POST /api/v1/orders (end-to-end checkout)

| Metric (`http_req_duration`, toàn bộ request trong iteration) | cache-on | cache-off |
| --- | --- | --- |
| p50 (med) | 10.80 ms | 10.20 ms |
| p95 | 28.20 ms | 26.52 ms |
| p99 | n/a | n/a |
| max | 406.23 ms | 406.48 ms |
| avg | 14.98 ms | 13.55 ms |
| throughput (tổng request/s) | 35.88 req/s | 35.59 req/s |
| error-rate (`http_req_failed`) | 0.09% | 0.13% |
| dropped_iterations | 0 (không xuất hiện trong metrics) | 0 |

Per-step trends (tách riêng từng bước trong iteration):

| Trend | cache-on avg/med/p95/max | cache-off avg/med/p95/max |
| --- | --- | --- |
| `order_login_duration` | 180.88 / 177.53 / 206.66 / 229.75 ms | 147.49 / 147.30 / 154.17 / 154.69 ms |
| `order_address_duration` | 17.90 / 6.75 / 61.43 / 176.41 ms | 9.30 / 4.25 / 12.47 / 101.34 ms |
| `order_cart_duration` | 8.95 / 8.57 / 16.11 / 83.13 ms | 8.33 / 8.14 / 14.57 / 51.01 ms |
| `order_place_duration` | 23.38 / 19.86 / 34.52 / 304.43 ms | 20.89 / 19.20 / 30.91 / 133.60 ms |
| `iteration_duration` | 53.41 / 41.32 / 76.19 / 778.73 ms | 47.73 / 40.45 / 71.64 / 478.11 ms |

`order_rate_limited_total` = 0 ở cả hai lần chạy (không bị 429).

## Nhận xét

1. **Search không được cache** — chỉ `GetProductById`/`GetProductBySlug`/`GetCategories` + dashboard admin dùng `IDistributedCacheService`. Sự khác biệt cache on/off ở `search` chỉ là nhiễu đo đạc: cả hai lần chạy đều giữ đúng ~50 req/s, p95 nằm trong khoảng 12.7–14.5 ms.
2. **Product detail**: cache-off không giữ được 100 req/s — 566 dropped iterations, throughput tụt xuống 90.58 req/s, có các đợt spike tới 6.59 s (max). p95 tăng gần gấp đôi so với cache-on (10.75 ms vs 5.58 ms), median cũng tăng (4.57 ms vs 3.01 ms). Cache-on giữ được 99.97 req/s với chỉ 3 dropped iterations — đây là endpoint mà việc so sánh cache on/off có ý nghĩa thật.
3. **Place-order**: cache không có ảnh hưởng đáng kể (luồng đặt hàng không dùng cache). Tỉ lệ lỗi 0.09% / 0.13% (`http_req_failed`) đến từ `DELETE /api/v1/cart` trả 409 (4 lần, tổng cộng cả hai lần chạy) và `POST /api/v1/orders` trả 400 (1 lần, ở lần chạy cache-off) theo log API — nguyên nhân là do script test: k6 `constant-arrival-rate` có thể cấp phát nhiều VU hơn `USERS=50`, khiến hai VU dùng chung một load-user và đụng độ (race condition) trên cùng một cart, **không phải lỗi phía server**. Đề xuất fix ở mục "Cách tái hiện" / item 4 dưới đây (đã áp dụng vào `place-order.js`).
4. Đây là baseline đo ở **mức tải cố định, vừa phải** trên laptop dev (Debug build, console logging bật) — không phải giới hạn capacity thật của hệ thống. Cần một bài step/ramping test (tăng dần rate tới khi hệ thống bão hoà) để tìm điểm giới hạn thật — khuyến nghị làm ở bước kế tiếp.

## Cách tái hiện

Xem `tests/Load/README.md` — mục "Chạy baseline (cache ON rồi cache OFF)". Các lệnh dùng để tạo số liệu trên:

```bash
cd tests/Load

# Cache ON (API khởi động với Caching__Enabled=true, chờ warm-up ~10s)
BASE_URL=http://host.docker.internal:8080 ./run.sh search cache-on
BASE_URL=http://host.docker.internal:8080 ./run.sh product-detail cache-on
BASE_URL=http://host.docker.internal:8080 USERS=50 ./run.sh place-order cache-on

# Restart API với Caching__Enabled=false, chờ warm-up ~10s, chạy lại với label khác
BASE_URL=http://host.docker.internal:8080 ./run.sh search cache-off
BASE_URL=http://host.docker.internal:8080 ./run.sh product-detail cache-off
BASE_URL=http://host.docker.internal:8080 USERS=50 ./run.sh place-order cache-off
```

File JSON/`stdout` gốc của các lần chạy này nằm ở `tests/Load/results/baseline/`.
