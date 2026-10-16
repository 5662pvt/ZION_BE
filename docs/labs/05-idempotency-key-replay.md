# Lab 05 — Idempotency-Key replay (tuần tự, đồng thời, đổi payload)

Script: `tests/Labs/lab05-idempotency-key-replay.mjs`

## Mục tiêu

Chứng minh `POST /api/v1/orders` với header `Idempotency-Key` hoạt động đúng theo đặc tả: (a) gọi
lại với cùng key + cùng body → trả lại đúng đơn cũ; (b) 10 request đồng thời cùng key mới → chỉ 1
đơn được tạo; (c) cùng key nhưng body khác → `409 Conflict`; (d) tổng số đơn chỉ tăng đúng bằng số
lần đặt hàng *thực sự* khác nhau, không tính các lần replay.

## Cơ chế trong code

- `PlaceOrderCommandHandler.Handle` (`src/Modules/Orders/ZIONShop.Orders.Application/Features/PlaceOrder/PlaceOrderCommandHandler.cs`):
  - `IIdempotencyLock.AcquireAsync(userId, key)` →
    `PostgresIdempotencyLock` (`src/Modules/Orders/ZIONShop.Orders.Infrastructure/Services/PostgresIdempotencyLock.cs`):
    `pg_try_advisory_lock` theo key dẫn xuất từ `(userId, key)`, **poll mỗi 100ms tối đa 10s** nếu
    đang bị giữ — tức là các request đồng thời cùng key **chờ**, không bị trả lỗi ngay.
  - Sau khi có lock: tính `fingerprint` (`IdempotencyFingerprint.Compute(addressId, paymentMethod,
    note, couponCode)`), tra `OrderIdempotencyKey` đã tồn tại chưa
    (`TryReplayAsync`): nếu đã có và fingerprint khớp → trả lại đơn cũ với
    `Replayed: true`; nếu fingerprint **không khớp** → `OrdersErrors.IdempotencyKeyReused`
    (`Error.Conflict` → HTTP 409).
  - Request thắng (chưa có key) tạo đơn mới, ghi `OrderIdempotencyKey` (unique index
    `(UserId, Key)`); nếu một request khác lọt qua do race hiếm (ví dụ timeout lock), insert thứ
    hai vi phạm unique index → bắt `DuplicateIdempotencyKeyException`, rollback phần đã đặt
    (coupon/gift), rồi `TryReplayAsync` lại để trả về đơn của người thắng thật.
  - `OrdersController.Place` (`src/Api/ZIONShop.Api/Controllers/OrdersController.cs:48`): nếu
    `result.Value.Replayed`, set header `Idempotent-Replayed: true` trên response.
- CORS expose `Idempotent-Replayed` trong header cho phép client đọc được
  (`Program.cs` → `WithExposedHeaders(...)`), không ảnh hưởng tới việc gọi trực tiếp bằng `fetch`
  từ Node (không qua CORS) nhưng vẫn là header hợp lệ đọc qua `Headers.get`.

## Cách chạy

```bash
node tests/Labs/lab05-idempotency-key-replay.mjs
```

## Các bước script làm

1. Start API (consumers ON để đơn còn tự settle, không ảnh hưởng tới assertion của lab này — lab
   chỉ kiểm tra tầng HTTP/DB của idempotency, không chờ đơn `Confirmed`).
2. Đăng nhập admin + `customer@zionshop.local`, seed 1 sản phẩm còn nhiều hàng (50), lấy số đơn
   hiện tại của customer làm baseline.
3. **(a)** Gọi `POST /orders` 2 lần tuần tự với cùng `Idempotency-Key` + cùng body — kiểm tra cùng
   order id, lần 2 có header `Idempotent-Replayed: true`.
4. **(b)** Gọi `POST /orders` 10 lần đồng thời (`Promise.all`) với cùng 1 `Idempotency-Key` **mới**
   — kiểm tra chỉ có 1 order id duy nhất xuất hiện trong 10 response, đúng 9/10 response có
   `Idempotent-Replayed: true`.
5. **(c)** Gọi `POST /orders` 2 lần tuần tự với cùng `Idempotency-Key` nhưng `note` khác nhau —
   lần 2 kỳ vọng `409`, mã lỗi `Orders.IdempotencyKeyReused`.
6. **(d)** So số đơn của customer trước/sau toàn bộ lab — kỳ vọng tăng đúng 3 (1 cho mỗi khối
   a/b/c), dù tổng cộng đã gọi 14 request `POST /orders`.

## Kỳ vọng

| Assertion | Kỳ vọng |
| --- | --- |
| (a) 2 lần gọi tuần tự, cùng key cùng body | Cùng order id; lần 2 có `Idempotent-Replayed: true` |
| (b) 10 lần gọi đồng thời, cùng key mới | Đúng 1 order id; đúng 9/10 response là replay |
| (c) cùng key, body khác | Lần 2 trả `409`, mã lỗi `Orders.IdempotencyKeyReused` |
| (d) tổng số đơn tăng thêm | Đúng 3 (không tính các lần replay) |

## Bằng chứng

Nguồn: [`docs/labs/evidence/lab05-idempotency-key-replay.json`](evidence/lab05-idempotency-key-replay.json).

- **Thời điểm chạy**: 2026-10-16, `07:10:49.530Z` → `07:11:04.323Z` (≈ 14.8s).
- **Verdict**: `PASS`.

| Assertion | Kỳ vọng | Thực tế | Kết quả |
| --- | --- | --- | --- |
| (a) cả 2 lần gọi tuần tự đều 2xx | 2xx, 2xx | 200, 200 | PASS |
| (a) cả 2 lần gọi trả cùng order id | `ec300eb8-…` | `ec300eb8-…` | PASS |
| (a) lần gọi đầu không phải replay | false | false | PASS |
| (a) lần gọi thứ hai là replay | true | true | PASS |
| (a) đúng 1 đơn được tạo cho key này | 1 | 1 | PASS |
| (b) mọi request đồng thời đều settle 2xx | true | true | PASS |
| (b) đúng 1 order id duy nhất trong 10 request đồng thời | 1 | 1 | PASS |
| (b) đúng 9/10 response là replay | 9 | 9 | PASS |
| (b) đúng 1 đơn được tạo cho key này | 2 (tổng lũy kế) | 2 | PASS |
| (c) lần gọi đầu thành công | 2xx | 200 | PASS |
| (c) lần gọi thứ hai (body khác, cùng key) trả `409` | 409 | 409 | PASS |
| (c) mã lỗi 409 là `Orders.IdempotencyKeyReused` | `Orders.IdempotencyKeyReused` | `Orders.IdempotencyKeyReused` | PASS |
| (d) tổng số đơn tăng đúng 3 qua (a)+(b)+(c), dù gọi 14 lần `POST /orders` | 3 | 3 | PASS |

Quan sát số liệu đáng chú ý:

- **(a)**: `a1 = { status: 200, orderId: ec300eb8-0d31-4d06-b0f0-bcaec63cd45c, replayed: false }`, `a2` trả cùng `orderId`, `replayed: true`. `countAfterA = 1`.
- **(b)**: 10/10 response `200`; `bDistinctOrderIds = [3e469b4b-7f14-433e-bf73-9024f142dd64]` (chỉ 1 order id duy nhất); `bReplayedCount = 9` (9 trong 10 request là replay của người thắng). `countAfterB = 2`.
- **(c)**: `c1 = { status: 200, orderId: 33fe759e-ca29-4720-a7c1-1edabb24c5c9 }`; `c2 = { status: 409, code: Orders.IdempotencyKeyReused, message: "Idempotency-Key đã được dùng cho một yêu cầu đặt hàng khác." }`. `countAfterC = 3`.
- **(d)**: `baselineOrderCount = 0` → `countAfterC = 3` — tăng đúng 3 đơn dù tổng 14 lần gọi HTTP (2 + 10 + 2) trong toàn bộ lab.

## Kết luận

Cả 13 assertion đều PASS, xác nhận `PostgresIdempotencyLock` (pg advisory lock theo `(userId, key)`,
poll 100ms/tối đa 10s) cùng unique index `(UserId, Key)` trên `OrderIdempotencyKey` và
`TryReplayAsync`-so-khớp-fingerprint hoạt động đúng đặc tả idempotency ở cả 3 kịch bản: gọi lại
tuần tự với cùng key/body luôn trả về đúng đơn cũ (`Idempotent-Replayed: true`); 10 request đồng
thời cùng key mới chỉ tạo đúng 1 đơn nhờ advisory lock serialize các request cùng key, 9 còn lại
nhận lại kết quả replay; đổi body nhưng giữ key bị chặn bởi fingerprint mismatch, trả `409
Orders.IdempotencyKeyReused`. Tổng số đơn tăng đúng bằng số lần đặt hàng thực sự khác nhau (3), không
tính các lần replay, dù đã gọi 14 request `POST /orders` — không có sai lệch nào so với kỳ vọng ban
đầu.
