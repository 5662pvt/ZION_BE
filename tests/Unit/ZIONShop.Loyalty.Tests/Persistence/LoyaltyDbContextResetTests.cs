using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Loyalty;
using ZIONShop.EventBus.Inbox;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Infrastructure.Persistence;

namespace ZIONShop.Loyalty.Tests.Persistence;

/// <summary>
/// <see cref="LoyaltyDbContext.ResetBusinessChanges"/> replaces a prior <c>ChangeTracker.Clear()"</c> that
/// also detached the pending <see cref="InboxMessage"/> row added by
/// <c>IdempotentIntegrationEventHandler</c> before calling the inner handler -- so a retry after a
/// concurrency/unique-index conflict committed its business change but never persisted the inbox row,
/// letting the same event be reprocessed on redelivery. These tests assert the selective-detach contract
/// the decorator relies on: the inbox row survives, everything else (including any queued outbox event)
/// does not.
/// </summary>
public class LoyaltyDbContextResetTests
{
    private static LoyaltyDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<LoyaltyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    [Fact]
    public void ResetBusinessChanges_Should_KeepInboxMessageAdded_When_BusinessEntitiesAndOutboxAreAdded()
    {
        using var db = CreateDb();
        var inboxEntry = db.Set<InboxMessage>().Add(new InboxMessage
        {
            MessageId = Guid.NewGuid(),
            Consumer = "Test.Consumer",
            EventType = "TestEvent",
            ProcessedOn = DateTime.UtcNow
        });
        var account = PointAccount.Create(Guid.NewGuid());
        account.Increase(10);
        db.PointAccounts.Add(account);
        db.AddOutboxEvent(new LoyaltyPointsEarnedIntegrationEvent(Guid.NewGuid(), 10, "Order", null, null, DateTime.UtcNow.AddMonths(1)));

        db.ResetBusinessChanges();

        inboxEntry.State.Should().Be(EntityState.Added);
        db.Entry(account).State.Should().Be(EntityState.Detached);
        db.Set<OutboxMessage>().Local.Should().BeEmpty();
        db.ChangeTracker.Entries().Should().ContainSingle().Which.Entity.Should().BeOfType<InboxMessage>();
    }

    [Fact]
    public async Task ResetBusinessChanges_Should_DetachModifiedBusinessEntity_When_AlreadyPersisted()
    {
        using var db = CreateDb();
        var account = PointAccount.Create(Guid.NewGuid());
        db.PointAccounts.Add(account);
        await db.SaveChangesAsync();

        account.Increase(5);
        var inboxEntry = db.Set<InboxMessage>().Add(new InboxMessage
        {
            MessageId = Guid.NewGuid(),
            Consumer = "Test.Consumer",
            EventType = "TestEvent",
            ProcessedOn = DateTime.UtcNow
        });

        db.ResetBusinessChanges();

        inboxEntry.State.Should().Be(EntityState.Added);
        db.Entry(account).State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public void ResetBusinessChanges_Should_NotThrow_When_NoInboxMessageIsTracked()
    {
        using var db = CreateDb();
        db.PointAccounts.Add(PointAccount.Create(Guid.NewGuid()));

        db.ResetBusinessChanges();

        db.ChangeTracker.Entries().Should().BeEmpty();
    }
}
