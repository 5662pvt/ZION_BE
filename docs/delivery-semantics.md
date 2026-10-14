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
2. Chưa có → `Add` một `InboxMessage` mới vào `TDbContext` (chưa `SaveChanges`), rồi gọi `THandler.HandleAsync(...)`.
3. Handler ném exception → gỡ (`Detach`) entry vừa thêm rồi rethrow — không gì được ghi, không gì được
   commit, event sẽ được giao lại và thử lại từ đầu.
4. Handler chạy xong không lỗi: nếu handler đã tự gọi `SaveChangesAsync` trên cùng `TDbContext` (qua uow
   scoped cùng DI scope) thì dòng inbox đã được commit **atomic** cùng thay đổi nghiệp vụ. Nếu entry vẫn
   còn ở trạng thái `Added` (handler không đụng tới `TDbContext` này, xem phần "Không atomic" bên dưới) thì
   decorator tự gọi `SaveChangesAsync` để dòng inbox vẫn được ghi lại cho lần giao sau.

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
