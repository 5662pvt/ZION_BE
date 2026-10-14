using MediatR;
using ZIONShop.Contracts.Auth;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Products;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Application.Interfaces;
using ZIONShop.Reviews.Application.Mappings;
using ZIONShop.Reviews.Application.Services;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Domain.Exceptions;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.CreateReview;

public class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, Result<ReviewDto>>
{
    private readonly IReviewRepository _reviews;
    private readonly IReviewsUnitOfWork _uow;
    private readonly ProductRatingSummaryUpdater _summaryUpdater;
    private readonly IPurchaseVerifier _purchaseVerifier;
    private readonly IUserContactReader _userContactReader;
    private readonly IProductCatalogReader _catalogReader;

    public CreateReviewCommandHandler(
        IReviewRepository reviews,
        IReviewsUnitOfWork uow,
        ProductRatingSummaryUpdater summaryUpdater,
        IPurchaseVerifier purchaseVerifier,
        IUserContactReader userContactReader,
        IProductCatalogReader catalogReader)
    {
        _reviews = reviews;
        _uow = uow;
        _summaryUpdater = summaryUpdater;
        _purchaseVerifier = purchaseVerifier;
        _userContactReader = userContactReader;
        _catalogReader = catalogReader;
    }

    public async Task<Result<ReviewDto>> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        var product = await _catalogReader.GetAsync(request.ProductId, cancellationToken);
        if (product is null || product.Status != "Published")
            return Result.Failure<ReviewDto>(ReviewsErrors.ProductNotAvailable);

        var existing = await _reviews.GetByUserAndProductAsync(request.UserId, request.ProductId, cancellationToken);
        if (existing is not null)
            return Result.Failure<ReviewDto>(ReviewsErrors.AlreadyReviewed);

        var orderId = await _purchaseVerifier.GetCompletedOrderIdAsync(request.UserId, request.ProductId, cancellationToken);
        if (orderId is null)
            return Result.Failure<ReviewDto>(ReviewsErrors.NotEligible);

        var contact = await _userContactReader.GetAsync(request.UserId, cancellationToken);
        var authorName = MaskAuthorName(contact);
        var content = request.Content.Trim();

        var review = Review.Create(request.ProductId, request.UserId, orderId.Value, request.Rating, request.Title, content, authorName, product.Name);

        await _reviews.AddAsync(review, cancellationToken);
        await _summaryUpdater.ApplyAsync(review.ProductId, review.Id, review.Rating, cancellationToken);

        return Result.Success(review.ToDto());
    }

    private static string MaskAuthorName(UserContact? contact)
    {
        if (contact is null) return "Người dùng ẩn danh";
        if (!string.IsNullOrWhiteSpace(contact.DisplayName)) return contact.DisplayName!;

        var local = contact.Email.Split('@')[0];
        var prefix = local.Length >= 2 ? local[..2] : local;
        return prefix + "***";
    }
}
