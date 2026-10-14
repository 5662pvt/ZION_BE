using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Inbox;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Inventory.Application.Interfaces;
using ZIONShop.Inventory.Domain.Entities;

namespace ZIONShop.Inventory.Infrastructure.Persistence;

public class InventoryDbContext : DbContext, IInventoryUnitOfWork
{
    public const string Schema = "inventory";

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
        modelBuilder.AddOutbox(Schema);
        modelBuilder.AddInbox(Schema);
    }

    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);

    /// <summary>
    /// If a transaction is already open on this DbContext (e.g. the IdempotentIntegrationEventHandler
    /// decorator wrapping this handler's delivery), a second real BeginTransactionAsync would throw --
    /// join the existing one via a SAVEPOINT instead. Only starts a real transaction when none is open.
    /// </summary>
    public async Task<IInventoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var currentTransaction = Database.CurrentTransaction;
        if (currentTransaction is not null)
        {
            var savepointName = $"sp_{Guid.NewGuid():N}";
            await currentTransaction.CreateSavepointAsync(savepointName, cancellationToken);
            return new EfInventoryTransaction(currentTransaction, savepointName);
        }

        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new EfInventoryTransaction(transaction);
    }
}
