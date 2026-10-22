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

    public void ClearChangeTracker() => ChangeTracker.Clear();
}
