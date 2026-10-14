namespace ZIONShop.Reviews.Domain.Entities;

public class ProductRatingSummary
{
    private ProductRatingSummary() { }

    public Guid ProductId { get; private set; }
    public int Count { get; private set; }
    public int Sum { get; private set; }
    public int Star1 { get; private set; }
    public int Star2 { get; private set; }
    public int Star3 { get; private set; }
    public int Star4 { get; private set; }
    public int Star5 { get; private set; }

    public decimal Average => Count == 0 ? 0m : Math.Round((decimal)Sum / Count, 1, MidpointRounding.AwayFromZero);

    public static ProductRatingSummary CreateEmpty(Guid productId) => new() { ProductId = productId };

    /// <summary>Replaces all counters from the full set of ratings that should currently count as Published for this product.</summary>
    public void Recompute(IReadOnlyCollection<int> publishedRatings)
    {
        Count = publishedRatings.Count;
        Sum = publishedRatings.Sum();
        Star1 = publishedRatings.Count(r => r == 1);
        Star2 = publishedRatings.Count(r => r == 2);
        Star3 = publishedRatings.Count(r => r == 3);
        Star4 = publishedRatings.Count(r => r == 4);
        Star5 = publishedRatings.Count(r => r == 5);
    }
}
