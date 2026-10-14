using ZIONShop.Reviews.Domain.Enums;
using ZIONShop.Reviews.Domain.Exceptions;
using ZIONShop.SharedKernel.Entities;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Domain.Entities;

public class Review : AggregateRoot
{
    private Review() { }

    public Guid ProductId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrderId { get; private set; }
    public int Rating { get; private set; }
    public string? Title { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string AuthorName { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public ReviewStatus Status { get; private set; }
    public string? HiddenReason { get; private set; }

    public static Review Create(Guid productId, Guid userId, Guid orderId, int rating, string? title, string content, string authorName, string productName)
    {
        return new Review
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            UserId = userId,
            OrderId = orderId,
            Rating = rating,
            Title = title,
            Content = content,
            AuthorName = authorName,
            ProductName = productName,
            Status = ReviewStatus.Published
        };
    }

    public void UpdateContent(int rating, string? title, string content)
    {
        Rating = rating;
        Title = title;
        Content = content;
        LastModifiedDate = DateTime.UtcNow;
    }

    public Result Hide(string? reason)
    {
        if (Status == ReviewStatus.Hidden) return Result.Failure(ReviewsErrors.AlreadyHidden);
        Status = ReviewStatus.Hidden;
        HiddenReason = reason;
        LastModifiedDate = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Show()
    {
        if (Status == ReviewStatus.Published) return Result.Failure(ReviewsErrors.AlreadyPublished);
        Status = ReviewStatus.Published;
        HiddenReason = null;
        LastModifiedDate = DateTime.UtcNow;
        return Result.Success();
    }
}
