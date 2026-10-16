# Lab 02 — Worker crash ngay trước khi ack message

Script: `tests/Labs/lab02-consumer-crash-before-ack.mjs`

## Mục tiêu

Chứng minh: nếu process Worker chết ngay sau khi handler xử lý xong một message nhưng **trước
khi** gửi ack cho RabbitMQ (mô phỏng crash thật — mất điện, OOM-kill, ...), message đó **không bị
mất** (RabbitMQ redeliver) và khi redeliver, hệ thống **không áp dụng lại hiệu ứng hai lần** nhờ
Inbox dedupe.

## Cơ chế trong code

- `FaultInjectionOptions.CrashBeforeAckEventType` (`src/BuildingBlocks/ZIONShop.EventBus/FaultInjection/FaultInjectionOptions.cs`)
  — chỉ hoạt động khi `IHostEnvironment.IsDevelopment()` (`CrashBeforeAckFaultInjector.ShouldCrashBeforeAck`).
- `RabbitMqConsumerService.OnMessageReceivedAsync` (`src/BuildingBlocks/ZIONShop.EventBus/RabbitMq/RabbitMqConsumerService.cs:308-317`):
  sau khi mọi handler cho event chạy xong **thành công**, nếu
  `_faultInjector.ShouldCrashBeforeAck(eventType)` đúng, gọi `_faultInjector.Crash(...)` →
  `EnvironmentProcessTerminator.Terminate` → `Environment.FailFast(...)`
  (`src/BuildingBlocks/ZIONShop.EventBus/FaultInjection/IProcessTerminator.cs`) — **không**
  `BasicAck`, không chạy `finally`/graceful shutdown, mô phỏng crash cứng thật sự.
- Message chưa ack: khi kết nối TCP của Worker tới broker đứt (process chết), RabbitMQ tự
  chuyển message đó về `ready` cho consumer kế tiếp (tại đây không có channel nào khác đang mở
  QoS, nên nó nằm trong `messages_unacknowledged` rồi về lại `messages_ready` khi broker phát hiện
  mất kết nối).
- Khi Worker khởi động lại (không có flag crash), nó consume lại message đó. Việc **không xử lý
  lại hiệu ứng nghiệp vụ hai lần** do `IdempotentIntegrationEventHandler<TEvent,THandler,TDbContext>`
  (`src/BuildingBlocks/ZIONShop.EventBus/Inbox/IdempotentIntegrationEventHandler.cs`): composite PK
  `(MessageId, Consumer)` trên bảng Inbox — lần xử lý thứ hai của cùng `EventId` bị chặn bởi check
  `AnyAsync` trước khi gọi handler thật.
- Event bị crash ngay sau khi xử lý: `OrderCreatedIntegrationEvent` — handler tương ứng
  (`OrderCreatedIntegrationEventHandler`, module Inventory) gọi
  `IInventoryItemRepository.TryReserveAsync` để giữ hàng. Nếu message được áp dụng hai lần mà
  không có Inbox, `Reserved` sẽ tăng gấp đôi.

### Log của API/Worker — mỗi lần chạy, mỗi process instance một file riêng

`tests/Labs/lib.mjs`'s `spawnDotnet` đặt tên file log là
`<tên lab>-<api|worker>-<thời điểm bắt đầu run này>-<số thứ tự lần start proc đó trong run>.log`
(ví dụ `lab02-consumer-crash-before-ack-worker-2026-10-16T07-27-40-123Z-1.log` cho Worker lần đầu,
`...-2.log` cho Worker lần hai). Quan trọng với lab này vì nó start Worker **hai lần** trong một
run (lần 1 crash, lần 2 xử lý redelivery) — nếu hai lần đó ghi vào cùng một file (tên cố định
`worker.log`, mở với flag `a` — append), và file đó cũng được **tái sử dụng giữa các lần chạy
script khác nhau** (không bị xoá/đổi tên giữa hai lần `node lab02...mjs`), log của lần chạy trước
còn sót lại từ lần chạy trước. Script tìm dòng `FAULT INJECTION: crashing before ACK for ...` bằng
`log.match(...)` (lấy match **đầu tiên**) sẽ lấy sai `messageId` — của lần chạy cũ, không phải lần
chạy hiện tại — khiến bước kiểm tra Inbox ở dưới tra nhầm message, thấy 0 dòng, và lab FAIL dù hệ
thống hoàn toàn đúng. Mỗi lần gọi `startWorker()`/`startApi()` giờ luôn ghi vào một file log mới
(tên duy nhất theo run + số lần start), và script chỉ đọc đúng `worker.logPath` của lần
`startWorker()` đầu tiên trong run hiện tại, đồng thời assert **đúng 1** dòng
`FAULT INJECTION` trong file đó (không phải ≥1) để phát hiện sớm nếu giả định "một file = một lần
crash" bị vi phạm.

### Eventual consistency — vì sao script phải poll inventory sau khi đơn đã `Confirmed`

Giống Lab 01: đơn chuyển `Confirmed` và Inventory thực sự **commit** (trừ `OnHand`, trả `Reserved`
về 0) là hai bước khác nhau, lệch nhau một nhịp outbox/consumer — và ở lab này, **Worker (lần
khởi động lại thứ hai) là host duy nhất chạy consumer** (API chạy với
`BackgroundProcessing__Enabled=false`), nên nó phải tiếp tục sống để tự xử lý cả hai bước:

1. Worker thứ hai redeliver + xử lý lại `OrderCreatedIntegrationEvent` → Inventory giữ hàng lại
   (`OrderCreatedIntegrationEventHandler`, chặn double-apply bởi Inbox) → phát
   `InventoryReservedIntegrationEvent`.
2. Orders nhận event đó
   (`InventoryReservedIntegrationEventHandler`,
   `src/Modules/Orders/ZIONShop.Orders.Application/IntegrationEvents/InventoryReservedIntegrationEventHandler.cs`),
   đánh dấu Order `Confirmed` — **đây là lúc `GET /orders/{id}` bắt đầu trả `Confirmed`** — rồi
   ghi `OrderConfirmedIntegrationEvent` vào outbox của Orders.
3. Outbox của Orders cần một nhịp poll/publish nữa, và chính Worker thứ hai đó phải consume lại để
   `OrderConfirmedIntegrationEventHandler`
   (`src/Modules/Inventory/ZIONShop.Inventory.Application/IntegrationEventHandlers/OrderConfirmedIntegrationEventHandler.cs`)
   chạy `TryCommitAsync` (trừ `OnHand`, `Reserved` về 0).

Nếu script dừng Worker thứ hai ngay sau khi thấy đơn `Confirmed` (bước 2) mà không chờ bước 3, nó
sẽ giết Worker trước khi inventory kịp commit và đọc sai số liệu. Script dùng `waitForInventory`
(`tests/Labs/lib.mjs`, poll `GET /inventory` mỗi 500ms tới 30s) **trong khối `try`, trước khi
`finally` dừng Worker thứ hai** — tức Worker thứ hai luôn còn sống trong lúc chờ bước 3 hoàn tất.

## Cách chạy

```bash
node tests/Labs/lab02-consumer-crash-before-ack.mjs
```

API chạy với `BackgroundProcessing__Enabled=false` (không consumer/outbox trên API); Worker chạy
hai lần: lần đầu với `FaultInjection__CrashBeforeAckEventType=OrderCreatedIntegrationEvent` (để
crash), lần hai không có flag (để xử lý redelivery).

## Các bước script làm

1. Start API (`BackgroundProcessing__Enabled=false`) + Worker (có flag crash).
2. Đăng nhập admin + customer, tạo sản phẩm published, seed stock = 10.
3. Đặt 1 đơn COD → `OrderCreatedIntegrationEvent` vào outbox Orders → Worker's
   `OutboxPublisherService<OrdersDbContext>` publish nó lên RabbitMQ → Worker's
   `RabbitMqConsumerService` consume, `OrderCreatedIntegrationEventHandler` (Inventory) chạy xong
   (giữ hàng thành công) → fault injector crash Worker trước ack.
4. Chờ Worker process tự thoát (ghi lại thời điểm thoát làm `crashAt`); đọc **đúng file log của
   lần `startWorker()` đầu tiên trong run này** (`worker.logPath` — xem mục "Log của API/Worker" ở
   trên), tìm toàn bộ dòng khớp `FAULT INJECTION: crashing before ACK for
   OrderCreatedIntegrationEvent <messageId>` và assert **đúng 1** dòng khớp (không phải ≥1) trước
   khi lấy `messageId` từ dòng đó.
5. Lấy mẫu `messages`/`messages_ready`/`messages_unacknowledged` của queue `zionshop.api` qua
   RabbitMQ Management API (`GET /api/queues/%2F/zionshop.api`) trong ~30s để quan sát message
   được redeliver (không mất); ghi lại mẫu đầu tiên có `messages_ready >= 1` và độ trễ so với
   `crashAt` (thời gian RabbitMQ phát hiện kết nối TCP của Worker chết rồi chuyển message về
   `ready`).
6. Start lại Worker (không có flag crash) → chờ đơn đạt `Confirmed`/`Cancelled`.
7. Kiểm tra Inbox qua `psql` (không qua `GET /api/v1/admin/system/inbox`, xem mục "Vì sao kiểm tra
   Inbox qua psql" ở dưới): đúng 1 row cho `messageId` đã crash, quan sát `ProcessedOn` của row đó
   và so với `crashAt` để xác nhận handler đã commit (business change + ghi Inbox) **trước** khi
   crash xảy ra.
8. Kiểm tra inventory cuối bằng `waitForInventory` (poll tới khi đạt giá trị hoặc hết 30s, xem mục
   "Eventual consistency" ở trên — Worker thứ hai vẫn đang chạy trong lúc chờ): `onHand` giảm đúng
   1, `reserved=0`.
9. Kiểm tra qua `psql` số dòng `inventory."StockReservations"` khớp `(orderId, productId)` của đơn
   bị crash — phải đúng 1, xác nhận redelivery không tạo reservation nhân đôi.

### Vì sao kiểm tra Inbox qua `psql`, không qua `GET /admin/system/inbox`

`adminInboxList` (`tests/Labs/lib.mjs`) gọi `GET /api/v1/admin/system/inbox?module=Inventory` rồi
**lọc phía client** theo `messageId`. Khi inbox của module Inventory đã có hàng nghìn row (môi
trường dev dùng chung, tích lũy qua nhiều lần chạy lab/seed), một trang `pageSize=200` có thể không
chứa row cần tìm — lab thấy 0 match và FAIL dù hệ thống hoàn toàn đúng (row vẫn tồn tại trong DB,
chỉ là không nằm trong trang đã lấy). Script dùng `countInboxMessages('inventory', messageId,
'OrderCreatedIntegrationEventHandler')` (`tests/Labs/lib.mjs`) — chạy trực tiếp qua `psql`:

```sql
SELECT COUNT(*), MIN("ProcessedOn")
FROM inventory.inbox_messages
WHERE "MessageId" = '<messageId>'::uuid
  AND "Consumer" LIKE '%OrderCreatedIntegrationEventHandler';
```

Bảng `inventory.inbox_messages` có PK composite `("MessageId", "Consumer")`, cột `MessageId` kiểu
`uuid` (cast `::uuid`), `Consumer` kiểu `character varying(256)` chứa full CLR type name
(`ZIONShop.Inventory.Application.IntegrationEventHandlers.OrderCreatedIntegrationEventHandler`) —
nên dùng `LIKE '%...Handler'` thay vì so khớp chính xác. Không giới hạn trang, không bị miss dù
inbox có bao nhiêu row.

## Kỳ vọng

| Assertion | Kỳ vọng |
| --- | --- |
| Worker crash | Đúng 1 dòng `FAULT INJECTION...` trong log của lần Worker đầu tiên, process thoát bất thường |
| Queue trong lúc Worker chết | Có mẫu với `messages_ready >= 1` hoặc `messages_unacknowledged >= 1` — message không mất |
| Sau cửa sổ quan sát | `messages_unacknowledged = 0` — message đã về lại `ready`, sẵn sàng redeliver |
| Sau khi Worker khởi động lại | Đơn đạt `Confirmed` |
| Inbox module Inventory (qua `psql`) | Đúng 1 row cho `messageId` bị crash (`processedBeforeCrash` chỉ là quan sát, không assert — xem mục bên dưới) |
| Inventory cuối (polled, ≤30s, Worker thứ hai vẫn sống) | `onHand` giảm đúng 1 đơn vị, `reserved=0` |
| StockReservations cho (orderId, productId) bị crash | đúng 1 dòng |

## Bằng chứng

Nguồn: [`docs/labs/evidence/lab02-consumer-crash-before-ack.json`](evidence/lab02-consumer-crash-before-ack.json).

- **Thời điểm chạy**: 2026-10-16, `11:13:02.689Z` → `11:13:51.987Z` (≈ 49.3s).
- **Verdict**: `PASS`.

| Assertion | Kỳ vọng | Thực tế | Kết quả |
| --- | --- | --- | --- |
| Đặt đơn thành công (2xx) | 2xx | 200 | PASS |
| Đúng 1 dòng `FAULT INJECTION` trong log Worker lần đầu | 1 | 1 | PASS |
| Dòng crash khớp đúng event type | `OrderCreatedIntegrationEvent` | `OrderCreatedIntegrationEvent` | PASS |
| Worker process thực sự crash (không phải graceful stop) | process exited | `code=2148734499 signal=null` | PASS |
| Message không mất — có mẫu `ready`/`unacked >= 1` sau crash | true | true | PASS |
| Cuối cửa sổ quan sát, `unacked = 0` (message đã về `ready`) | 0 | 0 | PASS |
| Đơn đạt `Confirmed` sau khi Worker khởi động lại và redeliver | `Confirmed` | `Confirmed` | PASS |
| Đúng 1 row Inbox cho message bị crash (không double-apply) | 1 | 1 | PASS |
| Inventory phản ánh đúng 1 lần commit | `onHand=9, reserved=0` | `onHand=9, reserved=0` | PASS |
| Đúng 1 dòng `StockReservation` cho (orderId, productId) | 1 | 1 | PASS |

Quan sát số liệu đáng chú ý:

- `workerExit.code = 2148734499` (`0x80131623` = `COR_E_FAILFAST`) — exit code CLR chuẩn cho `Environment.FailFast` trên Windows, không phải `70` (xem "Lưu ý cho người chạy" ở dưới).
- Dòng log crash: `FAULT INJECTION: crashing before ACK for OrderCreatedIntegrationEvent 8779ec4a-6a05-4880-afb0-33f796c21cde`, tại `crashAt = 2026-10-16T11:13:16.126Z`.
- `messageBecameReadyAgain`: RabbitMQ phát hiện Worker mất kết nối và chuyển message về `ready` tại `11:13:22.254Z`, trễ `delayFromCrashMs = 6128` (~6.1s) so với `crashAt` — đúng với cơ chế TCP-timeout-detection, không phải mất message.
- Inbox check (qua `psql`, module Inventory): `count = 1`, `minProcessedOn = "2026-10-16 11:13:15.561435+00"` — đúng 1 row cho message bị crash, không double-apply.
- `processedBeforeCrash = true` (`processedOnIso = 2026-10-16T11:13:15.561Z` < `crashAtIso = 2026-10-16T11:13:16.126Z`, sau khi sửa `parsePgTimestamp` trong `tests/Labs/lib.mjs` — xem "Lưu ý cho người chạy" ở dưới): handler đã commit transaction (giữ hàng + ghi Inbox row) **trước** khi `Environment.FailFast` xảy ra, nghĩa là lần redeliver bị Inbox dedupe chặn ngay từ check `AnyAsync`, không gọi lại handler thật. Đây vẫn chỉ là quan sát bổ sung, không phải assertion — cả `true`/`false` đều là kết quả hợp lệ của at-least-once delivery; thứ assertion riêng đã chứng minh là Inbox vẫn chỉ có đúng 1 row sau redelivery bất kể thứ tự này.
- `reservationCount = 1`, `inventoryAfter = { onHand: 9, reserved: 0 }` (10 seed − 1 đơn, sku `LAB-HVJGP9RS`) — không double-reserve từ redelivery.
- `inventorySettleMs = 1715` (~1.7s) — thời gian `waitForInventory` phải poll sau khi đơn đạt `Confirmed` để Inventory thực sự commit (`onHand`/`reserved` ổn định), đúng với cơ chế eventual consistency mô tả ở mục "Eventual consistency" trên.

## Kết luận

Cả 10 assertion đều PASS. Lab chứng minh at-least-once delivery của RabbitMQ (message bị requeue
khi consumer mất kết nối trước ack, `RabbitMqConsumerService.cs:308-317`) kết hợp với Inbox dedupe
theo `(MessageId, Consumer)` (`IdempotentIntegrationEventHandler.cs`) đủ để đảm bảo một message
được redeliver không áp dụng hiệu ứng nghiệp vụ hai lần: dù `Environment.FailFast` xảy ra ngay sau
khi handler xử lý xong (giữ hàng) nhưng trước `BasicAck`, Inbox vẫn chỉ có đúng 1 row và Inventory
chỉ bị trừ đúng 1 lần (`onHand=9, reserved=0`). Sau khi sửa lỗi parse timestamp Postgres trong
`tests/Labs/lib.mjs` (`parsePgTimestamp`), `processedBeforeCrash = true` ở lần chạy này xác nhận
trực tiếp — không còn là false negative — rằng transaction đã commit (`processedOnIso =
2026-10-16T11:13:15.561Z`) trước khi Worker crash (`crashAtIso = 2026-10-16T11:13:16.126Z`).

## Lưu ý cho người chạy (orchestrator)

`Environment.FailFast` trên Windows không trả về chính xác exit code `70` truyền vào
`IProcessTerminator.Terminate` (đó chỉ là một phần nội dung thông báo crash, không phải exit code
tiến trình) — script **không** assert exit code cụ thể, chỉ assert process thực sự thoát bất
thường + log có dòng `FAULT INJECTION`.

Exit code thực tế quan sát được khi chạy lab này trên Windows: `2148734499`
(`0x80131623` = `COR_E_FAILFAST`) — mã lỗi CLR chuẩn cho `Environment.FailFast`, không phải `70`
hay bội số của nó. Ghi lại ở đây để người chạy sau không nhầm đây là lỗi khác.
