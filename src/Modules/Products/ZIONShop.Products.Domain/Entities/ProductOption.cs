namespace ZIONShop.Products.Domain.Entities;

public class ProductOption
{
    private ProductOption() { }

    public int Position { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public List<string> Values { get; private set; } = new();

    public static ProductOption Create(int position, string name, IReadOnlyList<string> values) => new()
    {
        Position = position,
        Name = name.Trim(),
        Values = values.Select(v => v.Trim()).ToList()
    };
}
