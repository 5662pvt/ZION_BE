# tests/Labs — 5 lab tái hiện lỗi hệ thống (ZION_BE)

Mỗi lab là một script Node.js (`.mjs`, ESM, Node 20+, **không dùng npm package nào** — chỉ `fetch`
toàn cục và các module `node:*` built-in) tự:

1. khởi động `dotnet run` cho API (và Worker nếu lab cần) với biến môi trường riêng cho lỗi đang
   tái hiện,
2. gây ra lỗi (dừng container, crash process, bắn request đồng thời, ...),
3. gọi API thật để quan sát hệ thống phản ứng thế nào,
4. khôi phục lại trạng thái ban đầu trong khối `finally` (start lại container đã dừng, dừng mọi
   process đã spawn),
5. ghi bằng chứng (JSON) vào `docs/labs/evidence/labNN-*.json` và thoát với mã 0 (mọi assertion
   PASS) hoặc 1 (có assertion FAIL hoặc lỗi runtime).

## Yêu cầu trước khi chạy

- `dotnet build ZIONShop.slnx` đã chạy thành công trước đó (các lab dùng `dotnet run --no-build`).
- Docker stack đã `up` (`docker compose -f deploy/docker-compose.yml up -d postgres redis rabbitmq
  aspire-dashboard` — **không cần** `api`/`worker`/`nginx` vì lab tự chạy API/Worker bằng
  `dotnet run`, không dùng container của chúng).
- Cổng `8080` (API) và `8090` (Worker, chỉ lab 02 dùng) đang trống trên máy — dừng mọi `dotnet run`
  cũ trước khi chạy lab.
- Postgres cục bộ (`zionshop-postgres` trong Docker, không phải Neon) — **không bao giờ** trỏ các
  lab này vào Neon, vì lab dừng/khởi động lại container Postgres và có thể drop connection giữa
  chừng.
- Mỗi lab tự quản lý dữ liệu nó tạo ra (sản phẩm `LAB-xxxxxxxx`, địa chỉ mặc định của
  `customer@zionshop.local`/`load{N}@zionshop.local`); dữ liệu dev cũ còn sót lại trong DB là bình
  thường, các lab không dọn DB trước khi chạy.
- Lab 04 cần API được khởi động với `Seed__LoadTestCustomers=50` để có sẵn `load1@zionshop.local`
  .. `load50@zionshop.local` / `Load@123` — lab tự truyền biến này khi spawn `dotnet run`, không
  cần làm gì thêm.

## Chạy từng lab

```bash
node tests/Labs/lab01-rabbitmq-down.mjs
node tests/Labs/lab02-consumer-crash-before-ack.mjs
node tests/Labs/lab03-redis-down.mjs
node tests/Labs/lab04-50-requests-10-stock.mjs
node tests/Labs/lab05-idempotency-key-replay.mjs
```

Mỗi lab in log có timestamp ra console và ghi log đầy đủ của `dotnet run` vào
`tests/Labs/.logs/<tên lab>-<api|worker>-<thời điểm bắt đầu run>-<số lần start proc đó trong
run>.log` (không commit — xem `.gitignore`) — mỗi lần gọi `startApi()`/`startWorker()` luôn ra một
file mới, nên một lab start cùng proc nhiều lần trong một run (lab 02 start Worker hai lần) không
bị lần sau đè log lần trước, và hai lần chạy script khác nhau cũng không dùng chung file. Kết quả
chi tiết (từng bước, từng assertion, verdict PASS/FAIL) được ghi vào `docs/labs/evidence/labNN-*.json`,
đè lên lần chạy trước.

## Lab khôi phục gì sau khi chạy (trong `finally`)

| Lab | Gây lỗi | Khôi phục |
| --- | --- | --- |
| 01 | `docker stop zionshop-rabbitmq` | `docker start zionshop-rabbitmq` (nếu còn đang dừng) + dừng API |
| 02 | Worker crash qua `FaultInjection__CrashBeforeAckEventType` | dừng Worker (khoẻ mạnh, chạy lần 2) + Worker đã crash (no-op) + dừng API |
| 03 | `docker stop zionshop-redis` | `docker start zionshop-redis` (nếu còn đang dừng) + dừng API |
| 04 | Không gây lỗi hạ tầng, chỉ bắn 50 request đồng thời | dừng API |
| 05 | Không gây lỗi hạ tầng, chỉ test Idempotency-Key | dừng API |

Không lab nào xoá hoặc sửa dữ liệu đã tồn tại trong DB trước khi nó chạy; mỗi lần chạy lại chỉ tạo
thêm sản phẩm/đơn hàng mới (sku/idempotency-key gắn timestamp), an toàn để chạy lại nhiều lần.
