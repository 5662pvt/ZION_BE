using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Reviews;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Loyalty.Application.Interfaces;
using ZIONShop.Loyalty.Application.Options;
using ZIONShop.Loyalty.Domain.Enums;

namespace ZIONShop.Loyalty.Application.IntegrationEvents;

/// <summary>Awards the review bonus once per (UserId, ProductId) forever, via the idempotent (UserId, Review,
/// ProductId) lot.</summary>
public class ReviewCreatedIntegrationEventHandler : IIntegrationEventHandler<ReviewCreatedIntegrationEvent>
{
    private readonly ILoyaltyMutationService _mutations;
    private readonly IOptions<LoyaltyOptions> _options;

    public ReviewCreatedIntegrationEventHandler(ILoyaltyMutationService mutations, IOptions<LoyaltyOptions> options)
    {
        _mutations = mutations;
        _options = options;
    }

    public async Task HandleAsync(ReviewCreatedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        await _mutations.EarnAsync(@event.UserId, _options.Value.ReviewBonus, PointLotSource.Review, @event.ProductId.ToString(), null, @event.ProductId, "Đánh giá sản phẩm", cancellationToken);
    }
}
