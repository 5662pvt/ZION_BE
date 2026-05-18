# ZIONShop — AI Context Index

Tài liệu dự án được **chia nhỏ** để Claude/Cursor chỉ đọc phần liên quan task → tiết kiệm token, giảm sai sót.

## Cách dùng

1. Luôn đọc **[CLAUDE.md](../CLAUDE.md)** (index ngắn) trước.
2. Chỉ mở file trong `context/` tương ứng task (bảng dưới).
3. Khi làm **module cụ thể**, đọc thêm `context/modules/{module}.md` (nếu có).

## Bảng routing — đọc file nào?

| Task | Files bắt buộc | Files tùy chọn |
|------|----------------|-----------------|
| Scaffold solution / Foundation | `01-overview`, `02-architecture`, `06-backend-rules`, `12-roadmap` | `07-database`, `08-api` |
| Module Auth / JWT | `03-backend-structure`, `06-backend-rules`, `09-security`, `08-api` | `modules/auth.md` |
| Module Products / Categories | `03`, `05-module-layout`, `06`, `07-database`, `08-api` | `modules/products.md` |
| Module Cart | `03`, `05`, `06`, `08` | `modules/cart.md` |
| Module Orders / Checkout | `03`, `05`, `06`, `10-event-driven`, `11-inventory`, `08-api` | `modules/orders.md` |
| Module Inventory | `07-database`, `11-inventory`, `10-event-driven`, `06` | `modules/inventory.md` |
| Module Payments | `10-event-driven`, `09-security`, `08-api`, `06` | `modules/payments.md` |
| Frontend (bất kỳ feature) | `04-frontend-structure`, `08-api`, `01-overview` | module FE tương ứng |
| Integration events / RabbitMQ | `10-event-driven`, `02-architecture` | — |
| Tests / CI | `13-testing-git`, `12-roadmap` | — |
| Review / refactor toàn repo | `14-ai-instructions`, `02-architecture` | tất cả liên quan module |

## Thư mục

```
.ai/
├── README.md                 ← file này
├── context/
│   ├── 01-overview.md
│   ├── 02-architecture.md
│   ├── 03-backend-structure.md
│   ├── 04-frontend-structure.md
│   ├── 05-module-layout.md
│   ├── 06-backend-rules.md
│   ├── 07-database.md
│   ├── 08-api.md
│   ├── 09-security.md
│   ├── 10-event-driven.md
│   ├── 11-inventory.md
│   ├── 12-roadmap.md
│   ├── 13-testing-git.md
│   ├── 14-ai-instructions.md
│   ├── 15-sources.md
│   └── modules/              ← bounded context từng module
│       ├── auth.md
│       ├── products.md
│       ├── cart.md
│       ├── orders.md
│       ├── inventory.md
│       └── payments.md
```

**Quy tắc ưu tiên:** Nội dung trong `.ai/context/` và `CLAUDE.md` thắng `structure.txt` / `Funtion.txt` khi mâu thuẫn.
