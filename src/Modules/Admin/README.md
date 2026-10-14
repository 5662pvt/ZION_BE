# ZIONShop.Admin module

Status: **skeleton** — only project structure exists.

Phase: see CLAUDE.md section 13. This module is reserved for later phases and intentionally contains no entities/handlers yet.

When implementing:
1. Add Domain entities, value objects, repository interfaces under `ZIONShop.Admin.Domain`.
2. Add CQRS features under `ZIONShop.Admin.Application/Features/{UseCase}/`.
3. Add EF Core `DbContext`, configurations, and repositories under `ZIONShop.Admin.Infrastructure/Persistence/`.
4. Register DI in `AddAdminApplication` and `AddAdminInfrastructure`.
5. Wire into `ZIONShop.Api/Program.cs` and add migration with `dotnet ef migrations add Initial`.
