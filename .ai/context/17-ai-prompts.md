# 17 — Prompt ngắn (Claude / Cursor)

> Copy 1 dòng **BASE** + 1 dòng **TASK**. Chi tiết plan: [12-roadmap](12-roadmap.md), [16-admin](16-admin-portal-phases.md).

---

## BASE (ghim mọi prompt)

```
ZIONShop: .NET8 + React/Vite + SQL Server. Đọc CLAUDE.md + các file Read bên dưới. Một task/session. Domain→App→API→FE. Không PostgreSQL, không cross-DbContext, Categories trong Products.
```

---

## Trạng thái (2026-05)

| Xong | Thiếu |
|------|--------|
| Auth, Users, Products, Cart, FE storefront | RevokeToken, MergeCart, Update/Delete Category, Account forms |
| Plan admin A0–A7 | Admin UI, Orders, Inventory, Payments, Promotions |

**Tiếp theo:** Phase 1 gaps → Admin A0–A2 → Phase 2 (Inventory→Orders→Payments→Checkout).

---

## Prompt 1 dòng

### Tiếp tục (tự chọn 1 task)

```
BASE. Đọc: 12-roadmap, 16-admin §7, 17-ai-prompts §Trạng thái, 14-ai-instructions. Chọn 1 task kế tiếp (ưu tiên Phase 1 gaps → Admin A0 → Inventory), implement xong, liệt kê việc còn lại. Không commit.
```

### Phase 1 — Chốt (ngắn — khuyên dùng)

```
Hoàn thiện Phase 1 gaps trong ZIONShop. Đọc 17-ai-prompts.md mục "Phase 1 checklist" + CLAUDE.md + modules/auth,cart,products + 06,08.
Thứ tự: RevokeToken → MergeCart → Category PUT/DELETE → Archive/Publish product → Account forms.
BE→API→FE từng mục. Không P2/Admin. Không commit. Cuối: dotnet build.
```

### Phase 1 — Chốt (1 task / lần — ngắn nhất)

```
P1-{1-5} ZIONShop. Đọc 17-ai-prompts checklist + module liên quan + 06,08. BE→API→FE. Không commit.
```
Thay `{1-5}`: `1`=Revoke, `2`=MergeCart, `3`=Category, `4`=Archive, `5`=Account FE.

### Phase 1 checklist (Claude đọc file này — không paste vào chat)

| # | Task | Module |
|---|------|--------|
| 1 | RevokeRefreshToken + POST auth/revoke + FE logout | Auth |
| 2 | MergeGuestCart + FE sau login (X-Cart-Token) | Cart |
| 3 | UpdateCategory + DeleteCategory (Fail nếu có con/SP) | Products |
| 4 | ArchiveProduct + PublishProduct; Search ẩn Archived | Products |
| 5 | AccountPage: PUT /users/me + POST addresses | FE account |

### Phase 1 — từng task

| Task | Prompt |
|------|--------|
| Merge cart | `BASE. MergeGuestCart (Cart) + gọi sau login. Read: modules/cart.md, modules/auth.md, 06, 08.` |
| Logout API | `BASE. RevokeRefreshToken + POST auth/revoke + FE logout. Read: modules/auth.md, 09, 08.` |
| Category | `BASE. UpdateCategory + DeleteCategory + API AdminOnly. Read: modules/products.md, 06, 07, 08.` |
| Archive SP | `BASE. ArchiveProduct/PublishProduct + Search filter. Read: modules/products.md, 08.` |
| Account FE | `BASE. Form sửa profile + thêm địa chỉ AccountPage. Read: 04, 08, modules/auth.md.` |

### Admin

| Task | Prompt |
|------|--------|
| A0 Shell | `BASE. Admin A0: layout, sidebar, /admin routes, guard Admin/Staff. Read: 16 §A0, modules/admin.md, 04, 09.` |
| A1 Products | `BASE. Admin A1: ProductList + ProductForm. Read: 16 §A1, modules/products.md, 04, 08.` |
| A2 Categories | `BASE. Admin A2: cây category CRUD UI. Cần P1 Category BE. Read: 16 §A2, modules/products.md, 08.` |
| A3 Orders | `BASE. Admin A3: order list/detail. Cần Orders module. Read: 16 §A3, modules/orders.md, 10, 08.` |
| A4 KM | `BASE. Promotions module + Admin A4 screens. Read: modules/promotions.md, 16 §A4, 05–08.` |
| A5 Quà | `BASE. GiftRule + /admin/gifts. Cần A4. Read: modules/promotions.md, 16 §A5.` |
| A6 Kho | `BASE. Admin A6 stock + AdjustStock. Cần Inventory. Read: 11, modules/inventory.md, 16 §A6.` |
| A7 BC | `BASE. Admin A7 dashboard + revenue report. Read: modules/admin.md, 16 §A7, 07.` |

### Phase 2

| Task | Prompt |
|------|--------|
| Inventory | `BASE. Inventory MVP: reserve 15p, release, AdjustStock. Read: 11, modules/inventory.md, 10, 06, 07.` |
| Orders | `BASE. Orders: CreateOrder, lifecycle, events. Cần Inventory. Read: modules/orders.md, 10, 11, 08.` |
| Payments | `BASE. Payments MVP + PaymentCompleted event. Read: modules/payments.md, 10, 09, 08.` |
| Checkout FE | `BASE. Trang checkout + bật nút Cart. Cần Orders/Payments. Read: 04, modules/orders.md, 08.` |

### Khác

```
BASE. Implement {TênFeature} trong {Module}. Read: modules/{module}.md, 05, 06, 07, 08. Không làm: {phạm vi ngoài}.
```

```
Đọc CLAUDE + 02 + 14. Audit {Module} — liệt kê vi phạm layer/API. Không sửa code.
```

---

*Cập nhật mục Trạng thái sau mỗi task lớn.*
