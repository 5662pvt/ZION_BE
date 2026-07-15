# 12 — Implementation Roadmap & DevOps

## Development order (MUST)

| Step | Deliverable |
|------|-------------|
| 1 | Foundation (middleware, Result, ApiResponse, event bus) |
| 2 | SharedKernel + BuildingBlocks |
| 3 | Auth |
| 4 | Products (+ Categories) |
| 5 | Cart |
| 6 | Orders |
| 7 | Inventory |
| 8 | Payments |
| 9 | Notifications |
| 10 | Admin |

Complete **Domain → Application → API → tests** before matching frontend module.

## Phase plan

| Phase | Modules | Focus |
|-------|---------|-------|
| 1 | Auth, Users, Products, Cart | Browse, auth, cart |
| 2 | Orders, Inventory, Payments | Checkout |
| 3 | Promotions, Reviews, Notifications | Engagement |
| 4 | Admin, Analytics, Search | Operations |

**Admin Portal UI** (màn hình back-office) có lộ trình riêng theo phase A0–A7: [16-admin-portal-phases.md](16-admin-portal-phases.md). Có thể bắt A0–A2 song song khi Phase 1 gần xong.

Do **not** build all modules at once.

## CI/CD

**Backend:** Build → Test → Docker → Registry → Deploy

**Frontend:** Lint → Test → Build → Deploy

## Docker Compose (local)

Services: `api`, `sqlserver`, `redis`, `rabbitmq`, `seq`, `nginx`

Production: Kubernetes or Docker Swarm.

## Related docs

- Overview: [01-overview.md](01-overview.md)
- Foundation list: [06-backend-rules.md](06-backend-rules.md)
