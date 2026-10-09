using Microsoft.EntityFrameworkCore;
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
    }

    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);
}
