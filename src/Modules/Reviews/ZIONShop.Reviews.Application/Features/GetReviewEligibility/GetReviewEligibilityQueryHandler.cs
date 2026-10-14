using MediatR;
using ZIONShop.Contracts.Orders;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Application.Mappings;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.GetReviewEligibility;

public class GetReviewEligibilityQueryHandler : IRequestHandler<GetReviewEligibilityQuery, Result<ReviewEligibilityDto>>
{
    private readonly IReviewRepository _reviews;
    private readonly IPurchaseVerifier _purchaseVerifier;

    public GetReviewEligibilityQueryHandler(IReviewRepository reviews, IPurchaseVerifier purchaseVerifier)
    {
        _reviews = reviews;
        _purchaseVerifier = purchaseVerifier;
    }

    public async Task<Result<ReviewEligibilityDto>> Handle(GetReviewEligibilityQuery request, CancellationToken cancellationToken)
    {
        var existing = await _reviews.GetByUserAndProductAsync(request.UserId, request.ProductId, cancellationToken);
        if (existing is not null)
            return Result.Success(new ReviewEligibilityDto(false, "Bạn đã đánh giá sản phẩm này", existing.ToDto()));

        var orderId = await _purchaseVerifier.GetCompletedOrderIdAsync(request.UserId, request.ProductId, cancellationToken);
        if (orderId is null)
            return Result.Success(new ReviewEligibilityDto(false, "Bạn cần mua và nhận hàng sản phẩm này trước khi đánh giá", null));

        return Result.Success(new ReviewEligibilityDto(true, null, null));
    }
}
