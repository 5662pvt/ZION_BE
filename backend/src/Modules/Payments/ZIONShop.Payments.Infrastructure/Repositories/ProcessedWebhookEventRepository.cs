using Microsoft.EntityFrameworkCore;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Repositories;
using ZIONShop.Payments.Infrastructure.Persistence;

namespace ZIONShop.Payments.Infrastructure.Repositories;

public class ProcessedWebhookEventRepository : IProcessedWebhookEventRepository
{
    private readonly PaymentsDbContext _db;

    public ProcessedWebhookEventRepository(PaymentsDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        _db.ProcessedWebhookEvents.AnyAsync(p => p.EventId == eventId, cancellationToken);

    public async Task AddAsync(Guid eventId, DateTime processedAt, CancellationToken cancellationToken = default) =>
        await _db.ProcessedWebhookEvents.AddAsync(ProcessedWebhookEvent.Create(eventId, processedAt), cancellationToken);
}
