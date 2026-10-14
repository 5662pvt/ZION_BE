using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Payments.Application.Interfaces;
using ZIONShop.Payments.Domain.Entities;

namespace ZIONShop.Payments.Infrastructure.Persistence;

public class PaymentsDbContext : DbContext, IPaymentsUnitOfWork
{
    public const string Schema = "payments";

    private readonly Outbox<PaymentsDbContext> _outbox;

    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options)
    {
        _outbox = new Outbox<PaymentsDbContext>(this);
    }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PayableOrder> PayableOrders => Set<PayableOrder>();
    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);
        modelBuilder.AddOutbox(Schema);
    }

    public void AddOutboxEvent(IntegrationEvent @event) => _outbox.Add(@event);

    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);
}
