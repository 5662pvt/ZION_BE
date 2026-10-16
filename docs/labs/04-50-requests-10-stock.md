# Lab 04 — 50 request đặt hàng đồng thời, chỉ còn 10 hàng

Script: `tests/Labs/lab04-50-requests-10-stock.mjs`

## Mục tiêu

Chứng minh: khi 50 user đặt mua cùng 1 sản phẩm chỉ còn 10 hàng, **đúng 10** đơn thành công, 40
đơn còn lại hết hàng — không oversell, không lỗi 5xx — nhờ atomic conditional UPDATE ở tầng
Inventory, không phải nhờ việc kiểm tra tồn kho đồng bộ ở `PlaceOrderCommandHandler` (vốn chỉ là
pre-check, có thể bị race).

## Cơ chế trong code

- `PlaceOrderCommandHandler.Handle` có một pre-check đồng bộ
  (`GetInsufficientStockProductNamesAsync`, dựa trên `IStockAvailabilityReader`) — đây **chỉ** là
  lớp chặn sớm cho trường hợp rõ ràng vượt quá tồn kho tại thời điểm đọc; dưới tải đồng thời nhiều
  request có thể cùng đọc thấy "còn hàng" trước khi request nào giữ hàng thật.
- Giữ hàng thật diễn ra **bất đồng bộ**, sau khi đơn đã được tạo: `OrderCreatedIntegrationEvent` →
  Inventory module's `OrderCreatedIntegrationEventHandler`
  (`src/Modules/Inventory/ZIONShop.Inventory.Application/IntegrationEventHandlers/OrderCreatedIntegrationEventHandler.cs`)
  gọi `IInventoryItemRepository.TryReserveAsync`, một **UPDATE điều kiện atomic** duy nhất
  (`OnHand - Reserved >= quantity`) — không có cửa sổ đọc-rồi-ghi, nên 50 request giữ hàng đồng
  thời cho cùng sản phẩm không bao giờ xung đột hay cần retry loop.
- Sản phẩm hết hàng ở bước giữ hàng bất đồng bộ → `InventoryReservationFailedIntegrationEvent` →
  đơn chuyển `Cancelled` với `cancelReason="Hết hàng"` (constant `OutOfStockReason` trong
  `OrderCreatedIntegrationEventHandler`).
- `InventoryItem.OnHand`/`Reserved`/`Available` (`src/Modules/Inventory/ZIONShop.Inventory.Domain/Entities/InventoryItem.cs`)
  không bao giờ cho `Available < 0` vì `Reserve(quantity)` kiểm tra `Available < quantity` trước
  khi cộng `Reserved` — nhưng việc đó chỉ đúng nếu **chính UPDATE đó** là atomic, đó là lý do thật
  sự nằm ở `TryReserveAsync` (SQL), không nằm ở class này.

## Cách chạy

```bash
node tests/Labs/lab04-50-requests-10-stock.mjs
```

API chạy với `Seed__LoadTestCustomers=50` (seed `load1@zionshop.local`..`load50@zionshop.local` /
`Load@123` — xem `src/Modules/Auth/ZIONShop.Auth.Infrastructure/Persistence/AuthDbSeeder.cs`) và
rate limit auth/checkout được nâng lên để 50 login + 50 đặt hàng không bị 429 (mặc định
`RateLimiting:AuthPermitLimit=20`/60s theo IP — 50 login tuần tự từ `localhost` sẽ vượt mặc định).

## Các bước script làm

1. Start API với `Seed__LoadTestCustomers=50` + rate limit nâng cao.
2. Admin tạo 1 sản phẩm published, seed stock = 10.
3. Đăng nhập tuần tự `load1`..`load50`, mỗi user: lấy/tạo địa chỉ, dọn cart, thêm 1 sản phẩm vào
   cart.
4. Bắn đồng thời 50 request `POST /orders` (mỗi user 1 `Idempotency-Key` riêng) qua `Promise.all`
   (hàng rào bắt đầu chung).
5. Ghi lại histogram status code; không được có mã 5xx nào.
6. Với mọi response 2xx, poll `GET /orders/{id}` (bằng token của chính user đó) đến khi đơn đạt
   `Confirmed`/`Cancelled`, tối đa 45s mỗi đơn.
7. Đếm số đơn `Confirmed` (kỳ vọng = 10) và `Cancelled` (kỳ vọng = số đơn 2xx - 10).
8. Kiểm tra inventory cuối: `onHand=0`, `reserved=0`, không bao giờ âm.

## Kỳ vọng

| Assertion | Kỳ vọng |
| --- | --- |
| Không có lỗi 5xx | Đúng với toàn bộ 50 request |
| Mỗi request | 2xx (đơn được tạo) hoặc 409 (pre-check đồng bộ từ chối) — tổng = 50 |
| Số đơn `Confirmed` | Đúng 10 |
| Số đơn `Cancelled` | = (số đơn 2xx) − 10 |
| Inventory cuối | `onHand=0`, `reserved=0`, không bao giờ âm |

## Bằng chứng

Nguồn: [`docs/labs/evidence/lab04-50-requests-10-stock.json`](evidence/lab04-50-requests-10-stock.json).

- **Thời điểm chạy**: 2026-10-16, `07:28:41.803Z` → `07:29:26.320Z` (≈ 44.5s).
- **Verdict**: `PASS`.

| Assertion | Kỳ vọng | Thực tế | Kết quả |
| --- | --- | --- | --- |
| Không có lỗi 5xx dưới burst đồng thời | true | true | PASS |
| Mỗi request settle 2xx hoặc 409, tổng = 50 | 50 | 50 | PASS |
| Mọi đơn accepted đạt trạng thái cuối trong 45s | 0 (đơn còn pending) | 0 | PASS |
| Số đơn `Confirmed` | 10 | 10 | PASS |
| Số đơn `Cancelled` (hết hàng) | 40 | 40 | PASS |
| `onHand` về 0, `reserved` về 0 | `onHand=0, reserved=0` | `onHand=0, reserved=0` | PASS |
| Stock không bao giờ oversell (onHand âm) | >=0 | 0 | PASS |

Quan sát số liệu đáng chú ý:

- `statusHistogram = { "200": 50 }` — toàn bộ 50 request đều được chấp nhận (2xx) ở tầng đồng bộ, `syncRejectedCount = 0` — pre-check không chặn request nào (đúng với bản chất "chỉ là pre-check", race thật xảy ra ở tầng Inventory bất đồng bộ).
- `wallClockMs = 743` — 50 request `POST /orders` đồng thời (qua `Promise.all`) hoàn tất tầng đồng bộ trong 743ms; latency từng request dao động `712–740ms`.
- `finalStatusCounts = { confirmed: 10, cancelled: 40, stillPending: 0 }` — đúng 10 đơn giữ được hàng, 40 đơn còn lại `Cancelled` (lý do "Hết hàng"), không đơn nào kẹt `Pending`.
- `inventoryAfter = { onHand: 0, reserved: 0, available: 0, isLowStock: true }`, `inventorySettleMs = 60` — Inventory settle chỉ sau 60ms kể từ khi các đơn đã Confirmed/Cancelled, không âm.

## Kết luận

Cả 7 assertion đều PASS, chứng minh atomic conditional UPDATE trong `TryReserveAsync`
(`OnHand - Reserved >= quantity`) là cơ chế thật sự chống oversell, không phải pre-check đồng bộ
trong `PlaceOrderCommandHandler` (vốn chấp nhận cả 50/50 request vì tại thời điểm đọc "còn hàng").
Dưới tải 50 request giữ hàng đồng thời cho cùng 1 sản phẩm chỉ còn 10 đơn vị, đúng 10 đơn giành
được hàng (`Confirmed`) và 40 đơn còn lại tự chuyển `Cancelled` qua
`InventoryReservationFailedIntegrationEvent`, không có lỗi 5xx và `onHand` không bao giờ xuống âm.
Không có sai lệch nào so với kỳ vọng ban đầu.
