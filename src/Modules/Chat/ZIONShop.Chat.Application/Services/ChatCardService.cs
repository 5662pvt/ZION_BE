using ZIONShop.Chat.Application.DTOs;
using ZIONShop.Chat.Domain.Exceptions;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Products;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Services;

/// <summary>Resolves Product/Order cards from the cross-module Contracts at send time. The resolved
/// card is persisted as a JSON snapshot on the <c>ChatMessage</c> (see <c>ChatMappingExtensions</c>),
/// so reading messages back never needs to call these readers again.</summary>
public class ChatCardService
{
    private readonly IProductPriceLookup _products;
    private readonly IOrderDetailsReader _orders;

    public ChatCardService(IProductPriceLookup products, IOrderDetailsReader orders)
    {
        _products = products;
        _orders = orders;
    }

    public async Task<Result<(ChatProductCardDto Card, string ProductName)>> ResolveProductAsync(Guid productId, bool requirePublished, CancellationToken ct)
    {
        var summaries = await _products.GetProductSummaryAsync(new[] { productId }, ct);
        if (!summaries.TryGetValue(productId, out var info) || (requirePublished && !info.IsPublished))
            return Result.Failure<(ChatProductCardDto, string)>(ChatErrors.ProductNotFound);

        var maxPrice = info.MaxPrice > 0 ? info.MaxPrice : info.MinPrice;
        var card = new ChatProductCardDto(info.ProductId, info.Name, info.Slug, info.MinPrice, maxPrice, info.Currency, info.PrimaryImageUrl);
        return Result.Success((card, info.Name));
    }

    /// <summary><paramref name="ownerUserId"/> is the id the order must belong to -- the sending customer's
    /// own id, or (when staff attaches a card) the conversation's customer id.</summary>
    public async Task<Result<(ChatOrderCardDto Card, string OrderCode)>> ResolveOrderAsync(Guid orderId, Guid ownerUserId, CancellationToken ct)
    {
        var order = await _orders.GetAsync(orderId, ct);
        if (order is null || order.UserId != ownerUserId)
            return Result.Failure<(ChatOrderCardDto, string)>(ChatErrors.OrderNotFound);

        var firstLine = order.Lines.FirstOrDefault();
        var itemCount = order.Lines.Sum(l => l.Quantity);
        var card = new ChatOrderCardDto(order.OrderId, order.Code, order.Status, order.Total, order.Currency, firstLine?.ProductName ?? string.Empty, itemCount);
        return Result.Success((card, order.Code));
    }
}
