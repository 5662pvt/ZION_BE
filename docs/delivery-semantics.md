# Delivery semantics: Outbox + Inbox

## At-least-once, không phải exactly-once

`ZIONShop.EventBus` giao event theo kiểu **at-least-once**: một event có thể được giao nhiều hơn một lần
cho cùng một consumer. Lý do:

- **RabbitMQ**: nếu consumer xử lý xong nhưng crash trước khi `BasicAck`, hoặc nếu channel bị lỗi, broker sẽ
  giao lại message (retry/redelivery, xem `RabbitMqConsumerService`).
- **Outbox**: `OutboxPublisherService` poll bảng `outbox_messages` theo chu kỳ và publish các row chưa
  `ProcessedOn`. Nếu publish thành công nhưng tiến trình crash trước khi update `ProcessedOn`, lần poll kế
  tiếp sẽ publish lại row đó.

Không có cơ chế nào trong hệ thống (DB, broker, mạng) đảm bảo "exactly once" từ đầu đến cuối — bất kỳ consumer
nào không tự idempotent sẽ chạy lại business logic nhiều lần cho cùng một event.

## Nhiều instance cùng publish outbox (API + Worker, hoặc nhiều Worker)

`OutboxPublisherService<TDbContext>.PublishBatchAsync` có thể chạy đồng thời từ nhiều instance của
ứng dụng (ví dụ API host và một Worker host riêng, hoặc nhiều replica Worker) cùng trỏ vào một
database — mỗi instance poll theo chu kỳ riêng (`RabbitMq:OutboxPollSeconds`), không biết gì về các
instance khác. Để một row không bị publish trùng bởi hai instance cùng lúc, mỗi lần poll:

1. Mở một transaction DB tường minh trên `TDbContext`.
2. Lấy schema/table thật của `OutboxMessage` từ model (`db.Model.FindEntityType(typeof(OutboxMessage))`)
   — không hardcode tên schema/table, vì mỗi module tự đặt schema riêng.
3. Claim batch bằng raw SQL:
   `SELECT * FROM "<schema>"."outbox_messages" WHERE "ProcessedOn" IS NULL AND "Attempts" < @max ORDER BY "OccurredOn" LIMIT @batch FOR UPDATE SKIP LOCKED`.
   `FOR UPDATE SKIP LOCKED` khóa từng row được chọn trong phạm vi transaction này và bỏ qua (không
   chờ, không lỗi) bất kỳ row nào một transaction khác đang giữ khóa — nên hai instance chạy cùng
   lúc sẽ luôn claim hai tập row rời nhau (không giao nhau), không bao giờ cùng publish một row.
4. Publish từng message trong batch đã claim (logic retry/`BrokerUnavailableException` giữ nguyên
   như trước), rồi `SaveChangesAsync` + `CommitAsync` trên cùng transaction đó — khóa chỉ được giữ
   trong đúng thời gian xử lý batch này.

Đây **không** biến hệ thống thành exactly-once: nếu instance publish xong (broker đã nhận) nhưng
crash **trước khi** transaction commit, `ProcessedOn` sẽ không được ghi, row vẫn `null` và lần poll
kế tiếp (bởi instance nào còn sống) sẽ claim lại và publish lại — vẫn là at-least-once, chỉ là giữa
nhiều instance thay vì một. Inbox pattern ở phía consumer (xem phần dưới) vẫn là cơ chế chống trùng
duy nhất, không đổi.

## Outbox (producer) và Inbox (consumer)

Hai bảng riêng, hai vai trò riêng, mỗi module tự quản lý bảng của mình trong schema của nó:

- **Outbox** (`<schema>.outbox_messages`, `ZIONShop.EventBus.Outbox`): module **publish** event qua đây.
  `IOutbox<TDbContext>.Add(...)` chỉ `Add` một `OutboxMessage` vào `DbContext` đang có — commit cùng
  transaction với thay đổi nghiệp vụ sinh ra event đó. `OutboxPublisherService` poll bảng này, gọi
  `IEventBus.PublishAsync`, rồi đánh dấu `ProcessedOn`.
- **Inbox** (`<schema>.inbox_messages`, `ZIONShop.EventBus.Inbox`): module **consume** event qua đây. Mỗi
  row khóa chính kép `(MessageId, Consumer)` — `MessageId` = `IntegrationEvent.EventId` của event gốc,
  `Consumer` = `FullName` của handler cụ thể (một event có thể có nhiều consumer trong cùng module, mỗi
  consumer có dòng inbox riêng).

## `IdempotentIntegrationEventHandler<TEvent, THandler, TDbContext>`

Decorator trong `ZIONShop.EventBus.Inbox`, áp cho handler thật (`THandler`) khi đăng ký qua overload
`AddIntegrationEventHandler<TEvent, THandler, TDbContext>()` (khác với overload 2-type cũ, không có inbox,
giữ lại để tương thích nhưng có ghi chú "not idempotent").

Luồng xử lý:

1. Kiểm tra `(EventId, Consumer)` đã tồn tại trong `TDbContext.Set<InboxMessage>()` chưa. Có rồi → log
   `"duplicate {EventType} {EventId} skipped by {Consumer}"` rồi return, **không** gọi `THandler`.
2. Chưa có → `Add` một `InboxMessage` mới vào `TDbContext` (chưa `SaveChanges`).
3. Nếu `TDbContext.Database.CurrentTransaction` đang `null`, decorator tự mở MỘT transaction tường minh
   (`Database.BeginTransactionAsync`) bọc toàn bộ phần còn lại — kể cả mọi lần `SaveChangesAsync` mà
   `THandler` tự gọi trên cùng `TDbContext` này (một handler có thể gọi nhiều lần, ví dụ release coupon rồi
   release từng gift redemption trong loop). Nếu đã có transaction mở sẵn (handler tự `BeginTransactionAsync`
   trên cùng `TDbContext`, decorator phát hiện trước khi gọi `THandler`), decorator **không** mở transaction
   riêng — nó dựa vào handler join bằng SAVEPOINT, xem phần "Nested transaction" bên dưới.
4. Gọi `THandler.HandleAsync(...)`.
5. Handler ném exception (ở bất kỳ bước nào, kể cả sau khi đã tự `SaveChangesAsync` một hoặc nhiều lần) →
   nếu decorator sở hữu transaction, `RollbackAsync` toàn bộ (undo luôn các save đã "thành công" trước đó
   trong cùng lần giao này) → gỡ (`Detach`) entry inbox → rethrow. Không gì được ghi, không gì được commit,
   event sẽ được giao lại và thử lại từ đầu.
6. Handler chạy xong không lỗi: nếu entry inbox vẫn còn `Added` (handler không đụng tới `TDbContext` này,
   xem phần "Không atomic" bên dưới), decorator tự `SaveChangesAsync` để ghi dòng inbox. Sau đó, nếu
   decorator sở hữu transaction, `CommitAsync` — đây là lúc mọi thay đổi (dòng inbox + mọi save của handler)
   mới thực sự durable.

### Nested transaction (handler tự mở transaction)

Một handler như `Inventory.OrderCreatedIntegrationEventHandler` tự gọi `_uow.BeginTransactionAsync()` để
bọc logic reserve-stock/rollback-khi-hết-hàng của riêng nó. Vì decorator đã mở transaction trước khi gọi
handler, `InventoryDbContext.BeginTransactionAsync` phát hiện `Database.CurrentTransaction` khác `null` và
**join** bằng SAVEPOINT (`CreateSavepointAsync`) thay vì mở transaction thật thứ hai (sẽ throw). Với
`IInventoryTransaction` trả về từ nhánh này: `CommitAsync` là no-op (transaction ngoài của decorator mới
thực sự commit), `RollbackAsync` = `RollbackToSavepointAsync` (chỉ undo phần việc từ sau savepoint, không
ảnh hưởng dòng inbox hay bất kỳ thứ gì decorator đã ghi trước đó). Nhờ vậy, nhánh "hết hàng" của handler này
(rollback về savepoint rồi `SaveChangesAsync` cho `InventoryReservationFailedIntegrationEvent` + dòng inbox)
vẫn commit đúng — thông qua commit transaction ngoài của decorator, không phải transaction riêng của nó.

### Concurrency

Hai lần giao cùng `(EventId, Consumer)` đến gần như đồng thời: cả hai đều pass qua bước kiểm tra tồn tại ở
(1) (chưa commit nên chưa thấy nhau), cả hai đều `Add` row inbox rồi gọi handler. Khóa chính kép
`(MessageId, Consumer)` khiến request nào `SaveChangesAsync` sau sẽ vi phạm unique constraint — ngay trong
chính transaction đang chứa thay đổi nghiệp vụ của request đó — nên toàn bộ transaction đó fail, exception
ném ra, event được giao lại. Lần giao lại đó sẽ thấy row đã tồn tại (của request thắng) và bị skip ở bước
(1). Không cần code riêng cho Npgsql — bất kỳ provider quan hệ nào enforce PK đều cho cùng đảm bảo này.

## Danh sách consumer: atomic vs "inbox sau effect"

| Module | Event → Handler | Atomic với business change? |
|---|---|---|
| Cart | OrderCreated → OrderCreatedClearCartHandler | Có (cùng `ICartUnitOfWork`/`CartDbContext`) |
| Inventory | **OrderCreated → OrderCreatedIntegrationEventHandler** | **Không** — xem ghi chú bên dưới |
| Inventory | OrderConfirmed → OrderConfirmedIntegrationEventHandler | Có |
| Inventory | OrderCancelled → OrderCancelledIntegrationEventHandler | Có |
| Orders | InventoryReserved/-Failed/-Expired, PaymentCompleted/-Failed → tương ứng | Có (tất cả dùng `IOrdersUnitOfWork` cùng scope) |
| Payments | OrderCreated, InventoryReserved/-ReservationExpired/-ReservationFailed, OrderCancelled → tương ứng | Có |
| Promotions | OrderConfirmed/-Cancelled, InventoryReservationFailed → tương ứng | Có (`PromotionEvaluator`/`GiftPromotionEvaluator` tự `SaveChangesAsync` trên cùng `IPromotionsUnitOfWork`/scope) |
| Notifications | OrderConfirmed/-Shipped/-Cancelled → tương ứng | Có, nhưng có **side effect bên ngoài** (gửi email) — xem ghi chú |
| Users | UserRegistered → UserRegisteredIntegrationEventHandler | Có (qua `IUserProfileProvisioner`/`IUsersUnitOfWork` cùng scope) |

### Không atomic: `Inventory.OrderCreatedIntegrationEventHandler`

Handler này tự `IServiceScopeFactory.CreateScope()` nhiều lần (để retry khi gặp
`DbUpdateConcurrencyException`, cần nạp lại `xmin` token bằng `DbContext` mới mỗi lần thử). Các scope đó có
`InventoryDbContext` **riêng**, khác với `InventoryDbContext` mà decorator inbox đang giữ (scope ngoài do
`InMemoryEventBus`/`RabbitMqConsumerService` tạo). Vì vậy:

- Thay đổi nghiệp vụ (reserve stock, publish `InventoryReserved`/`InventoryReservationFailed` qua outbox)
  được commit trên `SaveChangesAsync` của scope **trong**.
  dòng inbox vẫn ở trạng thái `Added` trên `TDbContext` (scope ngoài) sau khi handler return.
- Decorator thấy entry còn `Added` → tự `SaveChangesAsync` — dòng inbox được ghi **sau** effect nghiệp vụ,
  không cùng transaction. Nếu tiến trình crash đúng giữa hai bước này, effect đã xảy ra nhưng dòng inbox
  chưa kịp ghi → lần giao lại sẽ chạy lại toàn bộ handler (handler tự idempotent ở mức nghiệp vụ nhờ kiểm
  tra `existing.Count > 0` đầu hàm, nên an toàn, nhưng đây không phải nhờ cơ chế inbox).

Một track khác đang refactor handler này để dùng chung scope — khi đó bảng này cần cập nhật lại.

### Side effect bên ngoài: email (Notifications)

`OrderConfirmedIntegrationEventHandler`, `OrderShippedIntegrationEventHandler`,
`OrderCancelledIntegrationEventHandler` gọi `IEmailSender.SendAsync(...)` **trước** khi `SaveChangesAsync`
(ghi `NotificationLog` + dòng inbox) chạy. Cả hai đều nằm trên cùng `NotificationsDbContext`/scope nên về
mặt DB là atomic — nhưng gửi email là một side effect không thể rollback. Nếu tiến trình crash sau khi email
đã gửi nhưng trước khi `SaveChangesAsync` commit, event sẽ được giao lại (vì không có dòng inbox), handler
chạy lại, và **email có thể bị gửi lại lần nữa**. Đây là caveat cố hữu của tích hợp email qua at-least-once
delivery — ngoài phạm vi sửa của inbox pattern (inbox chỉ đảm bảo DB-side effect, không đảm bảo side effect
ngoài DB).

## Fault injection: crash trước ACK (Development only)

Để một lab có thể tận mắt kiểm chứng tính at-least-once ở trên (chứ không chỉ đọc code), `RabbitMqConsumerService`
có một hook dev-only: cấu hình `FaultInjection:CrashBeforeAckEventType` = tên ngắn của event type (ví dụ
`OrderCreatedIntegrationEvent`, so khớp không phân biệt hoa/thường — đây luôn là dạng tên được dùng làm
routing key, xem `RabbitMqEventBus.PublishAsync`). Khi tên này khớp với event đang xử lý **và**
`IHostEnvironment.IsDevelopment()` là true, ngay sau khi mọi handler của message đó chạy xong (effect
nghiệp vụ + dòng inbox đã `SaveChangesAsync` thành công) nhưng **trước** `BasicAck`, tiến trình gọi
`Environment.FailFast` và chết ngay — không `BasicAck`, không chạy `finally`/`ProcessExit`/graceful shutdown
nào cả (xem `CrashBeforeAckFaultInjector`/`IProcessTerminator`). Vì message chưa được ack, broker sẽ giao
lại nó khi Worker khởi động lại mà không còn cấu hình này — đúng kịch bản "giao lại" ở mục đầu bài, và dòng
inbox đã ghi từ lần chạy trước sẽ khiến lần giao lại đó bị handler bỏ qua (dedupe), chứng minh inbox hoạt
động đúng dù process chết giữa lúc effect đã xảy ra và lúc ack.

Ở môi trường khác Development, giá trị này bị bỏ qua hoàn toàn (chỉ log Warning một lần lúc khởi động nếu
có set) — không có đường nào để kích hoạt ngoài Development.
