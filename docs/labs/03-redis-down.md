# Lab 03 — Redis sập trong khi cache sản phẩm đang nóng

Script: `tests/Labs/lab03-redis-down.mjs`

## Mục tiêu

Chứng minh: khi Redis sập, API **không bị chậm/treo** (không chờ timeout dài cho mỗi request) và
**không lỗi** — cache fail open, quay về đọc DB trực tiếp; đồng thời Idempotency-Key và rate
limiting (không phụ thuộc Redis) vẫn hoạt động bình thường.

## Cơ chế trong code

- `GetProductBySlugQueryHandler` (`src/Modules/Products/ZIONShop.Products.Application/Features/GetProduct/GetProductBySlugQueryHandler.cs`)
  cache kết quả 5 phút qua `IDistributedCacheService.GetOrCreateAsync` — route
  `GET /api/v1/products/by-slug/{slug}` (`AllowAnonymous`).
- `RedisCacheService.IsRedisUnavailable()` (`src/BuildingBlocks/ZIONShop.Caching/RedisCacheService.cs:156-163`):
  `true` ngay khi `_redis.IsConnected == false`, hoặc có lỗi Redis trong 5 giây gần nhất.
- `GetOrCreateAsync` (dòng 93-99): khi `IsRedisUnavailable()`, **bỏ qua** single-flight gate và
  `SetAsync`, gọi thẳng `factory(cancellationToken)` (đọc DB) cho **mỗi** caller — fail open đúng
  nghĩa, không serialize các request phía sau một cái lock vô dụng.
- `RedisHealthCheck` (`src/BuildingBlocks/ZIONShop.HealthChecks/RedisHealthCheck.cs`): Redis là
  optional, nên mất kết nối → `Degraded`, **không** `Unhealthy` — không kéo `/health/ready` xuống
  503.
- Idempotency-Key dùng `PostgresIdempotencyLock` (pg advisory lock) — không đụng Redis. Rate
  limiting dùng `RateLimitPartition.GetFixedWindowLimiter` của ASP.NET Core (in-memory, trong
  `Program.cs`) — cũng không đụng Redis.

## Cách chạy

```bash
node tests/Labs/lab03-redis-down.mjs
```

## Các bước script làm

1. Start API (cache mặc định `Caching:Enabled=true`).
2. Tạo + publish 1 sản phẩm, gọi `GET /products/by-slug/{slug}` 2 lần để warm cache.
3. `docker stop zionshop-redis`.
4. Gọi `GET /products/by-slug/{slug}` ngay sau khi Redis sập — đo latency, kỳ vọng vẫn 200 và
   <1000ms.
5. Gọi tiếp 20 request — mỗi request kỳ vọng <200ms, 200.
6. Gọi `GET /api/v1/admin/system/health` — xác nhận check `redis` = `Degraded`.
7. Đặt 1 đơn với `Idempotency-Key` trong lúc Redis sập — xác nhận vẫn 2xx (không phụ thuộc Redis).
8. `docker start zionshop-redis`, xác nhận cache hoạt động lại (200).

## Kỳ vọng

| Assertion | Kỳ vọng |
| --- | --- |
| Request đầu tiên sau outage | 200, <1000ms |
| 20 request tiếp theo | Mỗi request 200, <200ms |
| Health trong lúc outage | `redis` = `Degraded` (không phải `Unhealthy`) |
| Đặt hàng với Idempotency-Key trong lúc outage | 2xx (không phụ thuộc Redis) |
| Sau khi Redis lên lại | Cache hoạt động lại, 200 |

## Bằng chứng

Nguồn: [`docs/labs/evidence/lab03-redis-down.json`](evidence/lab03-redis-down.json).

- **Thời điểm chạy**: 2026-10-16, `07:11:21.552Z` → `07:11:35.061Z` (≈ 13.5s).
- **Verdict**: `PASS`.

| Assertion | Kỳ vọng | Thực tế | Kết quả |
| --- | --- | --- | --- |
| 2 lần warm-up cache đều thành công | 200 | 200, 200 | PASS |
| Request đầu tiên sau outage vẫn 200 | 200 | 200 | PASS |
| Request đầu tiên sau outage <1000ms (fail open, không timeout dài) | <1000ms | 39ms | PASS |
| 20 request tiếp theo đều 200 | true | true | PASS |
| 20 request tiếp theo đều <200ms | true | true | PASS |
| Admin health báo `redis` = `Degraded` lúc outage | `Degraded` | `Degraded` | PASS |
| Đặt đơn với Idempotency-Key vẫn 2xx lúc Redis sập | 2xx | 200 | PASS |
| `product-by-slug` hoạt động lại sau khi Redis reconnect | 200 | 200 | PASS |

Quan sát số liệu đáng chú ý:

- Warm-up latency: `miss = 63ms` (DB), `hit = 20ms` (Redis).
- `firstRequestAfterOutage`: `status=200`, `durationMs=39` — fail open tức thì, không chờ timeout.
- 20 request tiếp theo: latency dao động `6–24ms` (toàn bộ dưới ngưỡng 200ms).
- Health lúc outage: `redis = Degraded` (`durationMs=1056.7`, mô tả `"Redis unreachable"`), các check khác (`postgres`, `rabbitmq`) vẫn `Healthy` — Redis không kéo `/health/ready` xuống `Unhealthy` vì được coi là optional.
- Đặt đơn lúc Redis sập vẫn `200` (orderId `e8a329d7-ee47-41b9-8370-b1c30797cf27`) — Idempotency-Key dùng `PostgresIdempotencyLock`, không phụ thuộc Redis.
- Sau khi Redis reconnect: `afterReconnect = { status: 200, durationMs: 20 }` — cache hoạt động lại bình thường.

## Kết luận

Cả 8 assertion đều PASS, xác nhận cơ chế fail-open của `RedisCacheService.GetOrCreateAsync`
(`RedisCacheService.cs:93-99`) hoạt động đúng: khi `IsRedisUnavailable()` trả `true`, mọi caller bỏ
qua single-flight gate và đọc thẳng DB — request đầu tiên sau outage chỉ mất 39ms, 20 request kế
tiếp đều dưới 24ms, không có request nào bị treo chờ timeout Redis. `RedisHealthCheck` đúng như
thiết kế báo `Degraded` (không `Unhealthy`) vì Redis là optional, và các cơ chế độc lập với Redis —
`PostgresIdempotencyLock` cho Idempotency-Key, in-memory `FixedWindowRateLimiter` cho rate limiting
— vẫn hoạt động xuyên suốt outage. Không có sai lệch nào so với kỳ vọng ban đầu.
