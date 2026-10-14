namespace ZIONShop.Payments.Domain.Repositories;

public interface IProcessedWebhookEventRepository
{
    Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task AddAsync(Guid eventId, DateTime processedAt, CancellationToken cancellationToken = default);
}
