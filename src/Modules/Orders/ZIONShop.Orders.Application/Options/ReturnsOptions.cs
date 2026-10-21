namespace ZIONShop.Orders.Application.Options;

public class ReturnsOptions
{
    public const string SectionName = "Returns";

    public int WindowDays { get; set; } = 7;
}
