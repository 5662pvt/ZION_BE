# Labs tái hiện lỗi hệ thống — ZION_BE

5 lab tái hiện (reproduce) các kiểu lỗi phân tán thường gặp trong hệ thống event-driven của
ZIONShop (outbox/inbox, RabbitMQ, Redis cache, race condition trên stock, Idempotency-Key), chứng
minh cơ chế bảo vệ trong code thực sự hoạt động — không phải chỉ đọc code mà tin.

Script thực thi: `tests/Labs/*.mjs` (xem `tests/Labs/README.md` để biết cách chạy). Mỗi lab ghi
bằng chứng JSON vào `docs/labs/evidence/labNN-*.json` sau khi chạy.

| # | Lab | Lỗi gây ra | Cơ chế bảo vệ trong code | Kết quả mong đợi | Chi tiết | Kết quả (2026-10-16) |
| - | --- | --- | --- | --- | --- | --- |
| 01 | RabbitMQ sập giữa lúc đặt hàng | `docker stop zionshop-rabbitmq` trong lúc API đang xử lý đơn | Transactional Outbox: `OrderCreatedIntegrationEvent` được ghi vào bảng outbox cùng transaction với đơn hàng (`PlaceOrderCommandHandler.cs`); `OutboxPublisherService.PublishBatchAsync` (`src/BuildingBlocks/ZIONShop.EventBus/Outbox/OutboxPublisherService.cs:146-157`) bắt `BrokerUnavailableException` và dừng batch ngay, **không** tăng `Attempts` | Đơn vẫn được tạo (2xx) dù broker sập; outbox row ở trạng thái `Pending`, `Attempts=0`; khi broker lên lại, outbox tự publish, đơn tự Confirmed; không mất message, không double-reserve | [01-rabbitmq-down.md](01-rabbitmq-down.md) | **PASS** ([evidence](evidence/lab01-rabbitmq-down.json)) |
| 02 | Worker crash ngay trước khi ack message | `FaultInjection__CrashBeforeAckEventType` → `CrashBeforeAckFaultInjector.Crash()` gọi `Environment.FailFast` (`src/BuildingBlocks/ZIONShop.EventBus/FaultInjection/CrashBeforeAckFaultInjector.cs`) sau khi handler đã chạy xong, trước `BasicAck` (`RabbitMqConsumerService.cs:308-317`) | At-least-once delivery (RabbitMQ requeue message chưa ack) + Inbox dedupe theo `(MessageId, Consumer)` (`IdempotentIntegrationEventHandler.cs`) | Message được redeliver sau khi Worker khởi động lại; Inbox chỉ có đúng 1 row cho message đó; Reserved/OnHand chỉ bị áp dụng đúng 1 lần dù message redeliver | [02-consumer-crash-before-ack.md](02-consumer-crash-before-ack.md) | **PASS** ([evidence](evidence/lab02-consumer-crash-before-ack.json)) |
| 03 | Redis sập trong khi cache sản phẩm đang nóng | `docker stop zionshop-redis` | `RedisCacheService.IsRedisUnavailable()`/`GetOrCreateAsync` fail-open: khi multiplexer mất kết nối, bỏ qua cache, gọi thẳng DB, không chờ timeout dài, không serialize caller qua single-flight gate (`src/BuildingBlocks/ZIONShop.Caching/RedisCacheService.cs:93-99`) | Request đầu tiên sau khi Redis sập vẫn <1s, 200; 20 request tiếp theo đều <200ms, 200; health báo Redis `Degraded` (không phải `Unhealthy` — Redis là optional); đặt hàng với Idempotency-Key vẫn hoạt động (lock dùng Postgres, không phụ thuộc Redis) | [03-redis-down.md](03-redis-down.md) | **PASS** ([evidence](evidence/lab03-redis-down.json)) |
| 04 | 50 request đặt hàng đồng thời, chỉ còn 10 hàng | 50 user đặt 1 sản phẩm cùng lúc (`Promise.all`), sản phẩm chỉ có 10 hàng | Atomic conditional UPDATE trong `IInventoryItemRepository.TryReserveAsync` (`OnHand - Reserved >= quantity`) — không phải check-then-write ở application layer | Đúng 10 đơn `Confirmed`, phần còn lại `Cancelled` (hết hàng); không có request nào lỗi 5xx; `OnHand` cuối cùng = 0, `Reserved` = 0, không bao giờ âm | [04-50-requests-10-stock.md](04-50-requests-10-stock.md) | **PASS** ([evidence](evidence/lab04-50-requests-10-stock.json)) |
| 05 | Idempotency-Key replay (tuần tự, đồng thời, đổi payload) | Gọi lại `POST /orders` với cùng `Idempotency-Key` theo nhiều cách khác nhau | `PostgresIdempotencyLock` (pg advisory lock, serialize theo `(UserId,Key)`) + unique index `(UserId,Key)` trên `OrderIdempotencyKey` + `TryReplayAsync` so khớp fingerprint request (`IdempotencyFingerprint.Compute`) | Key giống + body giống → trả lại đúng 1 đơn (header `Idempotent-Replayed: true` ở các lần sau); 10 request đồng thời cùng key mới → đúng 1 đơn được tạo; key giống + body khác → `409 Orders.IdempotencyKeyReused`; tổng số đơn tăng đúng bằng số lần đặt hàng *thực sự* (không tính các lần replay) | [05-idempotency-key-replay.md](05-idempotency-key-replay.md) | **PASS** ([evidence](evidence/lab05-idempotency-key-replay.json)) |

## Bằng chứng

Mỗi file `docs/labs/evidence/labNN-*.json` là lần chạy **gần nhất** (bị ghi đè ở lần chạy sau),
gồm: `startedAt`/`finishedAt`, `environment` (biến môi trường đã truyền cho `dotnet run`), `steps`
(từng bước có timestamp), `observations` (dữ liệu quan sát được — response, health check, outbox
row, ...), `assertions` (từng assertion với `expected`/`actual`/`pass`), và `verdict` tổng
(`PASS`/`FAIL`).
