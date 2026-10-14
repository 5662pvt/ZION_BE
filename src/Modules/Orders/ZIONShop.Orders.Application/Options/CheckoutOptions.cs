namespace ZIONShop.Orders.Application.Options;

public class CheckoutOptions
{
    public const string SectionName = "Checkout";

    public decimal ShippingFee { get; set; } = 30000;
    public decimal FreeShippingThreshold { get; set; } = 500000;
    public string Currency { get; set; } = "VND";
}
