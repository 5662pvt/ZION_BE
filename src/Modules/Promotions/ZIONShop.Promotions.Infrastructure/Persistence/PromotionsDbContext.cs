using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Inbox;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Infrastructure.Persistence;

public class PromotionsDbContext : DbContext, IPromotionsUnitOfWork
{
    public const string Schema = "promotions";

    private readonly Outbox<PromotionsDbContext> _outbox;

    public PromotionsDbContext(DbContextOptions<PromotionsDbContext> options) : base(options)
    {
        _outbox = new Outbox<PromotionsDbContext>(this);
    }

    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<CouponCode> CouponCodes => Set<CouponCode>();
    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();
    public DbSet<GiftRedemption> GiftRedemptions => Set<GiftRedemption>();
    public DbSet<FlashSale> FlashSales => Set<FlashSale>();
    public DbSet<FlashSaleItem> FlashSaleItems => Set<FlashSaleItem>();
    public DbSet<FlashSaleRedemption> FlashSaleRedemptions => Set<FlashSaleRedemption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PromotionsDbContext).Assembly);
        modelBuilder.AddOutbox(Schema);
        modelBuilder.AddInbox(Schema);
    }

    public void AddOutboxEvent(IntegrationEvent @event) => _outbox.Add(@event);

    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);
}
