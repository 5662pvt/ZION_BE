# Lab 01 — RabbitMQ sập giữa lúc đặt hàng

Script: `tests/Labs/lab01-rabbitmq-down.mjs`

## Mục tiêu

Chứng minh: khi RabbitMQ sập đúng lúc một đơn hàng vừa được tạo, hệ thống **không mất message**
và **không double-reserve** stock khi broker lên lại — nhờ Transactional Outbox, không nhờ việc
publish trực tiếp đồng bộ trong request.

## Cơ chế trong code

- `PlaceOrderCommandHandler.Handle` (`src/Modules/Orders/ZIONShop.Orders.Application/Features/PlaceOrder/PlaceOrderCommandHandler.cs:200-219`)
  ghi `Order` + `OrderCreatedIntegrationEvent` vào outbox (`_uow.AddOutboxEvent(...)`) trong **cùng
  một** `SaveChangesAsync`/transaction — HTTP response trả về thành công ngay khi dòng này commit,
  không chờ broker.
- `OutboxPublisherService<TDbContext>.PublishBatchAsync` (`src/BuildingBlocks/ZIONShop.EventBus/Outbox/OutboxPublisherService.cs`)
  poll outbox mỗi `EventBus:OutboxPollSeconds` giây (mặc định 2s). Khi publish ném
  `BrokerUnavailableException` (dòng 146-157), nó `break` batch **ngay**, không tăng `Attempts`
  của message đang publish hay bất kỳ message nào sau nó trong batch — một outage dài hơn
  `MaxAttempts` (10) lần poll không bao giờ khiến message bị coi là `Failed` một cách oan uổng.
- Trạng thái outbox hiển thị qua `GET /api/v1/admin/system/outbox` (`AdminSystemController.cs`,
  policy `AdminOnly`) — `Pending` nghĩa là `ProcessedOn IS NULL AND Attempts < 10`
  (`OutboxAdminStore.StatusOf`).
- `RabbitMqHealthCheck`/`WorkerHealthCheck` (`src/BuildingBlocks/ZIONShop.HealthChecks/`) báo
  `Unhealthy` khi không kết nối được broker; lộ ra qua `GET /api/v1/admin/system/health`.
- Khi broker lên lại, `OutboxPublisherService` tiếp tục poll, publish thành công →
  `InventoryReservedIntegrationEvent`/`InventoryReservationFailedIntegrationEvent` →
  `OrderConfirmedIntegrationEvent` → đơn chuyển `Confirmed` (COD, đủ hàng).

### Eventual consistency — vì sao script phải poll inventory sau khi đơn đã `Confirmed`

Đơn chuyển `Confirmed` và Inventory thực sự **commit** (trừ `OnHand`, trả `Reserved` về 0) là hai
bước khác nhau, lệch nhau một nhịp outbox/consumer:

1. `OrderCreatedIntegrationEventHandler` (Inventory,
   `src/Modules/Inventory/ZIONShop.Inventory.Application/IntegrationEventHandlers/OrderCreatedIntegrationEventHandler.cs`)
   giữ hàng (`Reserved++`), phát `InventoryReservedIntegrationEvent`.
2. `InventoryReservedIntegrationEventHandler` (Orders,
   `src/Modules/Orders/ZIONShop.Orders.Application/IntegrationEvents/InventoryReservedIntegrationEventHandler.cs`)
   nhận event đó, đánh dấu Order `Confirmed` — **đây là lúc `GET /orders/{id}` bắt đầu trả
   `Confirmed`** — rồi ghi `OrderConfirmedIntegrationEvent` vào outbox của Orders.
3. `OrderConfirmedIntegrationEventHandler` (Inventory,
   `src/Modules/Inventory/ZIONShop.Inventory.Application/IntegrationEventHandlers/OrderConfirmedIntegrationEventHandler.cs`)
   chỉ chạy sau khi outbox của Orders poll/publish lại message đó và consumer xử lý nó — bước này
   mới thực sự `TryCommitAsync` (trừ `OnHand`, `Reserved` về 0).

Nói cách khác, giữa lúc đơn hiện `Confirmed` và lúc inventory phản ánh đúng số liệu cuối cùng có
một khoảng trễ bất định (poll interval + thời gian broker xử lý). Script dùng `waitForInventory`
(`tests/Labs/lib.mjs`) để poll `GET /inventory` mỗi 500ms tới khi đạt giá trị kỳ vọng hoặc hết
30s, thay vì đọc một lần ngay sau khi đơn `Confirmed` — đọc một lần dễ bắt trúng khoảng trễ này và
báo FAIL giả (`onHand`/`reserved` còn ở trạng thái "đã reserve, chưa commit").

## Cách chạy

```bash
node tests/Labs/lab01-rabbitmq-down.mjs
```

API được spawn với `BackgroundProcessing__Enabled=true` (consumer + outbox publisher chạy ngay
trên process API, không cần Worker riêng cho lab này).

## Các bước script làm

1. Start API (consumers ON), đăng nhập admin + `customer@zionshop.local`, tạo sản phẩm published,
   seed stock = 20.
2. **Baseline**: đặt 1 đơn khi RabbitMQ còn khoẻ, chờ đơn `Confirmed` — chứng minh happy path chạy
   được trước khi gây lỗi.
3. `docker stop zionshop-rabbitmq`.
4. Gọi `GET /api/v1/admin/system/health`, xác nhận check `rabbitmq` = `Unhealthy`.
5. Đặt thêm 1 đơn trong lúc broker sập — xác nhận vẫn 2xx.
6. Tìm row outbox module `Orders` khớp order này qua
   `GET /api/v1/admin/system/outbox?module=Orders&status=Pending` +
   `GET /api/v1/admin/system/outbox/Orders/{id}` — xác nhận `status=Pending`, `attempts=0`.
7. `docker start zionshop-rabbitmq`, đo thời gian đến khi đơn đạt `Confirmed`.
8. Kiểm tra inventory cuối cùng bằng `waitForInventory` (poll tới khi đạt giá trị hoặc hết 30s,
   xem mục "Eventual consistency" ở trên): `onHand=18` (20 - 2 đơn x 1), `reserved=0` — không
   double-reserve.
9. Kiểm tra qua `psql` số dòng `inventory."StockReservations"` khớp `(outageOrderId, productId)` —
   phải đúng 1, xác nhận không có reservation nhân đôi cho đơn đặt lúc broker sập.

## Kỳ vọng

| Assertion | Kỳ vọng |
| --- | --- |
| Đặt đơn baseline | 2xx, đơn `Confirmed` trong lúc broker khoẻ |
| Health khi broker sập | `rabbitmq` = `Unhealthy` |
| Đặt đơn khi broker sập | Vẫn 2xx (outbox ghi được, không chờ broker) |
| Outbox row của đơn đó | `Pending`, `attempts=0` (không bị phạt vì broker lỗi) |
| Sau khi broker lên lại | Đơn đạt `Confirmed` trong vòng 60s |
| Inventory cuối (polled, ≤30s) | `onHand=18`, `reserved=0` |
| StockReservations cho (outageOrderId, productId) | đúng 1 dòng |

## Bằng chứng

Nguồn: [`docs/labs/evidence/lab01-rabbitmq-down.json`](evidence/lab01-rabbitmq-down.json).

- **Thời điểm chạy**: 2026-10-16, `07:27:07.573Z` → `07:27:35.483Z` (≈ 27.9s).
- **Verdict**: `PASS`.

| Assertion | Kỳ vọng | Thực tế | Kết quả |
| --- | --- | --- | --- |
| Baseline order accepted | 2xx | 200 | PASS |
| Baseline order confirmed end-to-end (broker khoẻ) | `Confirmed` | `Confirmed` | PASS |
| Admin health báo `rabbitmq` unhealthy lúc broker sập | `Unhealthy` | `Unhealthy` | PASS |
| Đặt đơn vẫn được chấp nhận lúc broker sập | 2xx | 200 | PASS |
| Outbox row của đơn outage tồn tại, `Pending` | `Pending` | `Pending` | PASS |
| Outbox row không bị tăng `Attempts` lúc broker sập | 0 | 0 | PASS |
| Đơn phục hồi về `Confirmed` sau khi broker lên lại | `Confirmed` | `Confirmed` | PASS |
| Inventory nhất quán sau khi phục hồi | `onHand=18, reserved=0` | `onHand=18, reserved=0` | PASS |
| Đúng 1 dòng `StockReservation` cho (outageOrderId, productId) | 1 | 1 | PASS |

Quan sát số liệu đáng chú ý:

- `timeToRecoverMs = 11069` — từ lúc `docker start zionshop-rabbitmq` tới lúc đơn đạt `Confirmed`.
- `inventorySettleMs = 1653` — từ lúc đơn `Confirmed` tới lúc Inventory commit đúng số liệu cuối (xem mục "Eventual consistency" ở trên).
- Outbox row lúc outage: `status=Pending`, `attempts=0`, `processedOn=null` — đúng như cơ chế `break`-on-`BrokerUnavailableException` mô tả ở trên, không có record nào bị phạt vì broker lỗi.
- `outageReservationCount = 1`, `inventoryAfter = { onHand: 18, reserved: 0 }` (20 seed − 2 đơn × 1, cả baseline và outage order đều đã commit) — không double-reserve.

## Kết luận

Cả 9 assertion đều PASS, chứng minh Transactional Outbox hoạt động đúng như thiết kế: đơn hàng vẫn được tạo và trả về 2xx ngay cả khi RabbitMQ sập, vì `PlaceOrderCommandHandler` chỉ ghi outbox trong cùng transaction với `Order` (`PlaceOrderCommandHandler.cs:200-219`) mà không chờ broker. `OutboxPublisherService.PublishBatchAsync` (`OutboxPublisherService.cs:146-157`) `break` batch ngay khi gặp `BrokerUnavailableException` nên outbox row không bị tăng `Attempts` một cách oan uổng trong 11s outage — khi broker lên lại, outbox tự publish và đơn tự `Confirmed` chỉ sau ~1.65s để Inventory commit, không mất message và không double-reserve (đúng 1 `StockReservation`). Không có sai lệch nào so với kỳ vọng ban đầu.
