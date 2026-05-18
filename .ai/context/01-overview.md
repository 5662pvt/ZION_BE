# 01 — Overview & Technology Stack

**Project:** ZIONShop — Ecommerce Enterprise Platform

## Goals (MUST align)

- Production-ready, maintainable, scalable, high performance
- Modular Monolith — có thể tách microservices sau
- **AI-friendly:** predictable structure, stable naming, isolated modules

## Convention language

- **MUST** rules và code naming: **English**
- Giải thích ngữ cảnh: Vietnamese khi cần

## Technology Stack

| Layer | Technology | Notes |
|-------|------------|-------|
| Backend | .NET 8 Web API | Modular Monolith |
| Frontend | React 18+ + Vite | Feature-based modules |
| Database | **Microsoft SQL Server** | EF Core 8; **do NOT use PostgreSQL** |
| Cache | Redis | Distributed cache, locks |
| Message broker | RabbitMQ | Integration events |
| Auth | JWT + Refresh Token | RBAC |
| Logging | Serilog + Seq (local) | Structured logging |
| Deployment | Docker + CI/CD | Compose local; K8s/Swarm prod |

**MUST** read `ConnectionStrings:DefaultConnection` from config/environment — never hardcode.

## Domain-first order

Build: **Domain → Application → API → Integration tests → Frontend** (không build UI trước API core).

## Related docs

- Architecture: [02-architecture.md](02-architecture.md)
- Roadmap: [12-roadmap.md](12-roadmap.md)
