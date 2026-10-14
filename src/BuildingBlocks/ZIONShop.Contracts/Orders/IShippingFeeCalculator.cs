namespace ZIONShop.Contracts.Orders;

public interface IShippingFeeCalculator
{
    decimal Calculate(decimal subtotal);
}
