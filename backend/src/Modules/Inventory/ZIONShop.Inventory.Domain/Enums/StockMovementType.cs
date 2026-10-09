namespace ZIONShop.Inventory.Domain.Enums;

public enum StockMovementType
{
    Reserve = 0,
    Release = 1,
    Commit = 2,
    Adjust = 3,
    Expire = 4,
    Return = 5
}
