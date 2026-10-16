using Microsoft.EntityFrameworkCore;
using ZIONShop.Admin.Application.Features.ListAuditLogs;
using ZIONShop.Admin.Domain.Entities;
using ZIONShop.Admin.Infrastructure.Persistence;
using ZIONShop.Admin.Infrastructure.Repositories;

namespace ZIONShop.Admin.Tests.Features;

public class ListAuditLogsQueryHandlerTests
{
    private static (AdminDbContext Db, AuditLogRepository Repo) BuildScope()
    {
        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AdminDbContext(options);
        return (db, new AuditLogRepository(db));
    }

    private static AuditLog LogWith(
        string action = "Order.Ship", string resourceType = "Order", Guid? actorId = null, string? actorEmail = "admin@zionshop.local",
        DateTime? occurredAt = null) =>
        AuditLog.Create(
            occurredAt ?? DateTime.UtcNow, actorId ?? Guid.NewGuid(), actorEmail, action, resourceType, Guid.NewGuid().ToString(),
            "POST", "/api/v1/orders/x/ship", 200, "127.0.0.1", "corr-1", "trace-1");

    [Fact]
    public async Task Handle_Should_FilterByAction_When_ActionProvided()
    {
        var (db, repo) = BuildScope();
        var shipLog = LogWith(action: "Order.Ship");
        var completeLog = LogWith(action: "Order.Complete");
        await repo.AddAsync(shipLog);
        await repo.AddAsync(completeLog);
        await db.SaveChangesAsync();

        var handler = new ListAuditLogsQueryHandler(repo);
        var result = await handler.Handle(new ListAuditLogsQuery(1, 20, null, "Order.Ship", null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(l => l.Id == shipLog.Id);
    }

    [Fact]
    public async Task Handle_Should_FilterByActorId_When_ActorIsGuid()
    {
        var (db, repo) = BuildScope();
        var actorId = Guid.NewGuid();
        var mine = LogWith(actorId: actorId);
        var someoneElse = LogWith(actorId: Guid.NewGuid());
        await repo.AddAsync(mine);
        await repo.AddAsync(someoneElse);
        await db.SaveChangesAsync();

        var handler = new ListAuditLogsQueryHandler(repo);
        var result = await handler.Handle(new ListAuditLogsQuery(1, 20, actorId.ToString(), null, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(l => l.Id == mine.Id);
    }

    [Fact]
    public async Task Handle_Should_FilterByActorEmail_When_ActorIsNotGuid()
    {
        var (db, repo) = BuildScope();
        var mine = LogWith(actorEmail: "staff@zionshop.local");
        var someoneElse = LogWith(actorEmail: "admin@zionshop.local");
        await repo.AddAsync(mine);
        await repo.AddAsync(someoneElse);
        await db.SaveChangesAsync();

        var handler = new ListAuditLogsQueryHandler(repo);
        var result = await handler.Handle(new ListAuditLogsQuery(1, 20, "staff", null, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(l => l.Id == mine.Id);
    }

    [Fact]
    public async Task Handle_Should_OrderNewestFirst_When_NoFilterApplied()
    {
        var (db, repo) = BuildScope();
        var older = LogWith(occurredAt: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        var newer = LogWith(occurredAt: new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc));
        await repo.AddAsync(older);
        await repo.AddAsync(newer);
        await db.SaveChangesAsync();

        var handler = new ListAuditLogsQueryHandler(repo);
        var result = await handler.Handle(new ListAuditLogsQuery(1, 20, null, null, null, null, null), CancellationToken.None);

        result.Value.Items.Select(l => l.Id).Should().ContainInOrder(newer.Id, older.Id);
    }
}
