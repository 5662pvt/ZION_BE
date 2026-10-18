using ZIONShop.Reviews.Domain.Exceptions;
using ZIONShop.SharedKernel.Entities;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Domain.Entities;

/// <summary>Single-entity Q&amp;A shape: one question, at most one official answer. See module README for why this was chosen over a separate ProductAnswer child entity.</summary>
public class ProductQuestion : AggregateRoot
{
    private ProductQuestion() { }

    public Guid ProductId { get; private set; }
    public Guid UserId { get; private set; }
    public string AuthorName { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public string? AnswerContent { get; private set; }
    public DateTime? AnsweredAt { get; private set; }
    public Guid? AnsweredByUserId { get; private set; }

    public static ProductQuestion Create(Guid productId, Guid userId, string authorName, string content)
    {
        return new ProductQuestion
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            UserId = userId,
            AuthorName = authorName,
            Content = content
        };
    }

    /// <summary>Overwrites any existing answer rather than rejecting a second answer — staff editing the answer is the common case.</summary>
    public Result Answer(Guid answeredByUserId, string content)
    {
        var trimmed = content?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return Result.Failure(ReviewsErrors.AnswerContentRequired);

        AnswerContent = trimmed;
        AnsweredAt = DateTime.UtcNow;
        AnsweredByUserId = answeredByUserId;
        LastModifiedDate = DateTime.UtcNow;
        return Result.Success();
    }
}
