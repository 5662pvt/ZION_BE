# ZIONShop.Notifications module

Status: **skeleton** — only project structure exists.

Phase: see CLAUDE.md section 13. This module is reserved for later phases and intentionally contains no entities/handlers yet.

When implementing:
1. Add Domain entities, value objects, repository interfaces under `ZIONShop.Notifications.Domain`.
2. Add CQRS features under `ZIONShop.Notifications.Application/Features/{UseCase}/`.
3. Add EF Core `DbContext`, configurations, and repositories under `ZIONShop.Notifications.Infrastructure/Persistence/`.
4. Register DI in `AddNotificationsApplication` and `AddNotificationsInfrastructure`.
5. Wire into `ZIONShop.Api/Program.cs` and add migration with `dotnet ef migrations add Initial`.
