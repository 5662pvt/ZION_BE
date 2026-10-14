namespace ZIONShop.Products.Domain.ValueObjects;

public sealed record Sku
{
    public string Value { get; }

    private Sku(string value) { Value = value; }

    public static Sku Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) throw new ArgumentException("SKU cannot be empty", nameof(raw));
        var normalized = raw.Trim().ToUpperInvariant();
        if (normalized.Length is < 3 or > 64)
            throw new ArgumentException("SKU must be between 3 and 64 characters", nameof(raw));
        return new Sku(normalized);
    }

    public override string ToString() => Value;
}
