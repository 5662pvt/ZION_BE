using ZIONShop.Admin.Application.Features.GetTopCategories;
using ZIONShop.Admin.Application.Features.GetTopProducts;

namespace ZIONShop.Admin.Tests.Features;

public class ReportQueryValidatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void TopProductsValidator_Should_Fail_When_LimitOutOfRange(int limit)
    {
        var validator = new GetTopProductsQueryValidator();
        var result = validator.Validate(new GetTopProductsQuery(null, null, limit, "revenue"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void TopProductsValidator_Should_Fail_When_SortByInvalid()
    {
        var validator = new GetTopProductsQueryValidator();
        var result = validator.Validate(new GetTopProductsQuery(null, null, 10, "bogus"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void TopProductsValidator_Should_Pass_When_InputIsValid()
    {
        var validator = new GetTopProductsQueryValidator();
        var result = validator.Validate(new GetTopProductsQuery(null, null, 10, "quantity"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void TopCategoriesValidator_Should_Fail_When_SortByInvalid()
    {
        var validator = new GetTopCategoriesQueryValidator();
        var result = validator.Validate(new GetTopCategoriesQuery(null, null, "bogus"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void TopCategoriesValidator_Should_Pass_When_SortByIsNull()
    {
        var validator = new GetTopCategoriesQueryValidator();
        var result = validator.Validate(new GetTopCategoriesQuery(null, null, null));

        result.IsValid.Should().BeTrue();
    }
}
