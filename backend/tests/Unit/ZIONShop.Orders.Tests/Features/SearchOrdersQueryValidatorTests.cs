using ZIONShop.Orders.Application.Features.SearchOrders;

namespace ZIONShop.Orders.Tests.Features;

public class SearchOrdersQueryValidatorTests
{
    private readonly SearchOrdersQueryValidator _validator = new();

    [Fact]
    public void Validate_Should_Fail_When_FromIsAfterTo()
    {
        var query = new SearchOrdersQuery(1, 20, null, new DateTime(2026, 10, 10), new DateTime(2026, 10, 1), null);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Should_Pass_When_FromIsBeforeOrEqualTo()
    {
        var query = new SearchOrdersQuery(1, 20, null, new DateTime(2026, 10, 1), new DateTime(2026, 10, 10), null);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }
}
