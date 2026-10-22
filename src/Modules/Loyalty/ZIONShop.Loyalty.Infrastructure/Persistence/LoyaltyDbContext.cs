using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Inbox;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Loyalty.Application.Interfaces;
using ZIONShop.Loyalty.Domain.Entities;

namespace ZIONShop.Loyalty.Infrastructure.Persistence;

public class LoyaltyDbContext : DbContext, ILoyaltyUnitOfWork
{
    public const string Schema = "loyalty";

    private readonly Outbox<LoyaltyDbContext> _outbox;

    public LoyaltyDbContext(DbContextOptions<LoyaltyDbContext> options) : base(options)
    {
        _outbox = new Outbox<LoyaltyDbContext>(this);
    }

    public DbSet<PointAccount> PointAccounts => Set<PointAccount>();
    public DbSet<PointLot> PointLots => Set<PointLot>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();
    public DbSet<PointReservation> PointReservations => Set<PointReservation>();
    public DbSet<OrderPointsRevocation> OrderPointsRevocations => Set<OrderPointsRevocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoyaltyDbContext).Assembly);
        modelBuilder.AddOutbox(Schema);
        modelBuilder.AddInbox(Schema);
    }

    public void AddOutboxEvent(IntegrationEvent @event) => _outbox.Add(@event);

    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);

    /// <summary>Detaches every tracked entity except <see cref="InboxMessage"/>, so a retry re-fetches
    /// fresh state instead of re-applying mutations on now-stale entities, while leaving the inbox row
    /// (normally still <see cref="EntityState.Added"/>) for the idempotency decorator to persist once the
    /// retried attempt succeeds. A full <c>ChangeTracker.Clear()</c> would detach that row too, so a
    /// retried delivery would commit its business change without ever recording the inbox row, letting
    /// the same event be reprocessed on redelivery. This also detaches any <see cref="OutboxMessage"/>
    /// queued by the failed attempt, so a successful retry queues it exactly once.</summary>
    public void ResetBusinessChanges()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.Entity is not InboxMessage).ToList())
            entry.State = EntityState.Detached;
    }
}
