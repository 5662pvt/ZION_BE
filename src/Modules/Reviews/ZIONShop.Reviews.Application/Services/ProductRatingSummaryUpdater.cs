using Microsoft.EntityFrameworkCore;
using ZIONShop.Reviews.Application.Interfaces;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Domain.Enums;
using ZIONShop.Reviews.Domain.Repositories;

namespace ZIONShop.Reviews.Application.Services;

/// <summary>
/// Recomputes <see cref="ProductRatingSummary"/> from scratch and persists it together with whatever
/// review mutation the caller already staged on the shared unit of work, inside the same SaveChanges call.
/// The review being created/updated/deleted/hidden/shown is always excluded from the DB read and instead
/// folded back in from <paramref name="includedRating"/> — this sidesteps EF's "added/removed rows aren't
/// visible to a fresh query against the same context" gotcha regardless of which operation triggered it.
/// </summary>
public class ProductRatingSummaryUpdater
{
    private const int MaxAttempts = 3;

    private readonly IReviewRepository _reviews;
    private readonly IProductRatingSummaryRepository _summaries;
    private readonly IReviewsUnitOfWork _uow;

    public ProductRatingSummaryUpdater(IReviewRepository reviews, IProductRatingSummaryRepository summaries, IReviewsUnitOfWork uow)
    {
        _reviews = reviews;
        _summaries = summaries;
        _uow = uow;
    }

    public async Task ApplyAsync(Guid productId, Guid affectedReviewId, int? includedRating, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var otherRatings = await _reviews.Query()
                .Where(r => r.ProductId == productId && r.Id != affectedReviewId && r.Status == ReviewStatus.Published)
                .Select(r => r.Rating)
                .ToListAsync(ct);

            if (includedRating.HasValue) otherRatings.Add(includedRating.Value);

            var summary = await _summaries.GetByIdAsync(productId, ct);
            if (summary is null)
            {
                summary = ProductRatingSummary.CreateEmpty(productId);
                await _summaries.AddAsync(summary, ct);
            }

            summary.Recompute(otherRatings);

            try
            {
                await _uow.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < MaxAttempts)
            {
                foreach (var entry in ex.Entries) await entry.ReloadAsync(ct);
            }
        }
    }
}
