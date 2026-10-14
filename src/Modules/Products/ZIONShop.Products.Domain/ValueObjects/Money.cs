namespace ZIONShop.Products.Domain.ValueObjects;

public sealed record Money(decimal Amount, string Currency)
{
    public static Money Zero(string currency = "USD") => new(0m, currency);

    public Money Add(Money other)
    {
        if (Currency != other.Currency) throw new InvalidOperationException("Cannot add Money values with different currencies.");
        return this with { Amount = Amount + other.Amount };
    }

    public static Money Create(decimal amount, string currency = "USD")
    {
        if (amount < 0) throw new ArgumentException("Money amount cannot be negative", nameof(amount));
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be 3-letter ISO code", nameof(currency));
        return new Money(amount, currency.ToUpperInvariant());
    }
}
