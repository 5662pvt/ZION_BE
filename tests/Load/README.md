# k6 Load Test — Cache ON vs Cache OFF Baseline

Bộ script k6 đo baseline hiệu năng 3 luồng chính: search/list sản phẩm, xem chi tiết sản phẩm
(đường cached), và đặt hàng end-to-end (checkout). Mục tiêu: so sánh **cache Redis ON vs OFF**
trên cùng một máy/Docker local, không phải benchmark production.

## Vì sao chỉ product-detail là so sánh cache có ý nghĩa

Theo `IDistributedCacheService`, chỉ các query sau có cache (TTL 5 phút):
`GetProductById`, `GetProductBySlug`, `GetCategories` (+ admin dashboard). **Search/list sản
phẩm (`GET /api/v1/products?keyword=...`) KHÔNG được cache** — baseline của `search.js` phải
được đọc như "chi phí query Postgres không cache" ở cả hai lần chạy, không phải một so sánh
cache on/off. So sánh cache on/off có ý nghĩa nhất nằm ở `product-detail.js`.

## Yêu cầu trước khi chạy

1. Docker stack chạy đủ: Postgres, Redis, RabbitMQ (xem `deploy/docker-compose.yml`). API chạy
   trên host, lắng nghe `:8080` (k6 trong container gọi qua `http://host.docker.internal:8080`).
2. API chạy với các biến môi trường sau (ví dụ qua `appsettings.Development.json` hoặc env
   trước khi `dotnet run` / `docker compose up api`):

   | Biến | Giá trị cho load test | Vì sao |
   | --- | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Development` | bắt buộc để seed `Seed__LoadTestCustomers` chạy |
   | `Seed__LoadTestCustomers` | `50` | tạo `load1@zionshop.local` … `load50@zionshop.local` / `Load@123` |
   | `Caching__Enabled` | `true` rồi `false` | tắt/mở cache Redis cho GetProductById/BySlug/Categories |
   | `RateLimiting__AuthPermitLimit` | ví dụ `100000` | login lặp lại nhiều lần trong cùng window |
   | `RateLimiting__CheckoutPermitLimit` | ví dụ `100000` | đặt hàng lặp lại nhiều lần/user |
   | `RateLimiting__SearchPermitLimit` | ví dụ `100000` | **xem lưu ý IP dưới đây** |
   | `RateLimiting__WindowSeconds` | giữ `60` hoặc theo nhu cầu | cửa sổ fixed-window |

   **Lưu ý quan trọng về rate limit theo IP**: k6 chạy trong container Docker, gọi API qua
   `host.docker.internal`, nên tất cả VU đều xuất hiện với **cùng một IP nguồn** (gateway của
   Docker network) theo góc nhìn của server. Policy `search` và `auth` (`AuthRateLimitKeyResolver`)
   khóa theo IP → tổng traffic của toàn bộ VU cộng lại dùng chung một bucket, không phải mỗi VU
   một bucket. Vì vậy `SearchPermitLimit`/`AuthPermitLimit` phải đủ lớn cho **tổng** rate mong
   muốn (ví dụ `RATE=50` req/s trong 60s ⇒ cần permit ≥ 3000 cho cửa sổ đó, nên đặt dư nhiều hơn).
   Policy `checkout` khóa theo `user:{userId}` khi đã đăng nhập, nên không bị ảnh hưởng tương tự.

3. Database có sẵn catalog sản phẩm đã publish (cho `search.js` / `product-detail.js`) — nếu
   catalog rỗng, script dừng ngay với lỗi rõ ràng ở bước `setup()`.

## Chạy baseline (cache ON rồi cache OFF)

```bash
cd tests/Load

# 1) Cache ON — khởi động API với Caching__Enabled=true, chờ warm-up ~10s rồi chạy:
BASE_URL=http://host.docker.internal:8080 ./run.sh search cache-on
BASE_URL=http://host.docker.internal:8080 ./run.sh product-detail cache-on
BASE_URL=http://host.docker.internal:8080 USERS=50 ./run.sh place-order cache-on

# 2) Restart API với Caching__Enabled=false, chờ warm-up ~10s rồi chạy lại với label khác:
BASE_URL=http://host.docker.internal:8080 ./run.sh search cache-off
BASE_URL=http://host.docker.internal:8080 ./run.sh product-detail cache-off
BASE_URL=http://host.docker.internal:8080 USERS=50 ./run.sh place-order cache-off
```

Mỗi lần chạy ghi ra:

- stdout: text summary chuẩn của k6 (`handleSummary` + `textSummary` từ `jslib.k6-summary`).
- `tests/Load/results/<script>-<label>.json`: toàn bộ metrics dạng JSON (do orchestrator copy
  các file cần giữ vào `results/baseline/` sau khi chạy xong — đây là thư mục duy nhất được
  commit, phần còn lại trong `results/` bị `.gitignore`).

Biến môi trường override được ở mọi script: `RATE` (req/s), `DURATION` (ví dụ `60s`),
`PREALLOC_VUS`, `MAX_VUS`. `place-order.js` thêm: `USERS` (số load user, mặc định 50, phải khớp
`Seed__LoadTestCustomers`), `PRODUCT_COUNT` (số sản phẩm load-test tạo ra, mặc định 20),
`LOAD_USER_PASSWORD` (mặc định `Load@123`), `ADMIN_EMAIL`/`ADMIN_PASSWORD` (mặc định
`admin@zionshop.local` / `Admin@123`).

## Các script

- `k6/lib.js` — `BASE_URL`, header JSON/Bearer, hàm `login()` dùng chung, `randomPick()`.
- `k6/setup-data.js` — bootstrap gọi từ `setup()` của từng script: `adminLogin()`,
  `fetchCatalogSample()` (lấy slug/id sản phẩm đã publish có sẵn), `createLoadTestProducts()`
  (tạo N sản phẩm `LT-<timestamp>` stock 1,000,000 — chỉ dùng cho `place-order.js`).
- `k6/search.js` — `GET /api/v1/products?keyword=...&page=...&pageSize=20`, từ khoá lấy từ tên
  sản phẩm thật trong catalog (fallback về danh sách tiếng Việt tĩnh nếu catalog quá ít), scenario
  `constant-arrival-rate` mặc định 50 req/s / 60s.
- `k6/product-detail.js` — 80% `GET /api/v1/products/by-slug/{slug}`, 20%
  `GET /api/v1/products/{id}`, trên slug/id lấy từ catalog thật. Mặc định 100 req/s / 60s.
- `k6/place-order.js` — mỗi VU: login một lần bằng `load{(VU-1 % USERS)+1}@zionshop.local`,
  đảm bảo có address (tạo nếu chưa có), mỗi iteration: `DELETE /api/v1/cart` (dọn cart cũ) →
  thêm 1-2 sản phẩm load-test vào cart → `POST /api/v1/orders` (COD, `Idempotency-Key` mới mỗi
  lần). Mặc định 10 req/s / 60s. 429 được đếm riêng (metric `order_rate_limited_total`), không
  tính vào `http_req_failed`, để lộ rõ nếu rate limit bị đặt sai cho lần chạy đó.
  **`maxVUs` (mặc định = `USERS`) không được vượt `USERS`**: mỗi VU map 1-1 sang 1 load user
  theo `__VU`, nếu `maxVUs > USERS` thì hai VU dùng chung một user và đụng độ (race) trên cùng
  một cart/order của user đó — `setup()` dừng ngay với lỗi rõ ràng nếu bị đặt sai. Baseline
  2026-10-16 bị đúng lỗi này (`maxVUs=60 > USERS=50`) — xem `docs/perf/baseline.md` Nhận xét #3.

## Ý nghĩa các metric trong summary

- `http_req_duration` (p50/p95/p99): latency toàn bộ request HTTP (bao gồm TCP/TLS/transfer),
  tổng hợp mọi loại call trong script.
- Custom `Trend` theo từng endpoint (tag `name`): `products_search_duration`,
  `product_detail_by_slug_duration`, `product_detail_by_id_duration`, và ở `place-order.js`:
  `order_login_duration`, `order_address_duration`, `order_cart_duration`,
  `order_place_duration` — tách riêng call "measured" (search/detail/đặt hàng) khỏi các call phụ
  trợ (login/address/cart) để không bị pha loãng latency.
- `http_reqs`: throughput thực tế (request/s) — so với `RATE` cấu hình để biết server có theo
  kịp hay không (nếu `http_reqs` rate < `RATE` nghĩa là server/network nghẽn, VU bị giữ lại).
- `http_req_failed`: tỉ lệ request lỗi (status không 2xx/3xx theo định nghĩa mặc định của k6).
- `order_rate_limited_total`: số lần `POST /orders` bị 429 — nếu > 0 một cách bất ngờ, kiểm tra
  lại `RateLimiting__CheckoutPermitLimit`/`WindowSeconds` trước khi tin vào số liệu đặt hàng.

Cả 3 script đều set `options.summaryTrendStats = ['avg','min','med','max','p(90)','p(95)','p(99)']`
nên `p(99)` có trong summary/JSON kể từ lần chạy này (baseline 2026-10-16 chạy trước khi thêm
dòng này nên không có p99 — `docs/perf/baseline.md` ghi "n/a" cho các bảng đó).

Thresholds trong mỗi script (`options.thresholds`) chỉ để **báo cáo** (không có `abortOnFail`,
giá trị cố ý nới rộng) — một threshold "fail" không dừng run giữa chừng, chỉ hiển thị trong
summary cuối cùng.

## Lưu ý / hạn chế

- Chạy trên một máy, Docker Desktop local — số liệu không đại diện cho production (network,
  CPU contention với chính Docker Desktop, v.v.).
- Giao dịch: `src/Worker` (RabbitMQ consumer xác nhận đơn) có thể là tiến trình riêng hoặc chạy
  trong API (`BackgroundProcessing__Enabled`) — nếu tắt, đơn hàng có thể kẹt ở trạng thái chưa
  confirm; không ảnh hưởng tới latency đo ở `POST /orders` (lệnh trả về ngay sau khi ghi DB) nhưng
  ảnh hưởng tới trạng thái cuối của đơn nếu bạn kiểm tra sau đó.
- `place-order.js` tạo đơn hàng thật trong DB mỗi iteration — DB sẽ phình ra theo số lần chạy.
  Không có cơ chế dọn dẹp tự động trong script này; nếu cần dọn, hủy (`POST /orders/{id}/cancel`)
  hoặc xoá theo `note='k6 load test'` / sản phẩm có SKU bắt đầu `LT-`.
- Vì rate limit `search`/`auth` khóa theo IP và mọi VU dùng chung một IP nguồn khi chạy qua
  Docker (`host.docker.internal`), `RATE` cấu hình cao mà `SearchPermitLimit` không được nâng
  tương ứng sẽ tạo ra 429 hàng loạt — không phản ánh hiệu năng thật của endpoint.
