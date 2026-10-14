using ZIONShop.Users.Application.Features.AddAddress;
using ZIONShop.Users.Application.Features.UpdateAddress;

namespace ZIONShop.Users.Tests.Features;

public class AddressValidatorTests
{
    [Fact]
    public void AddAddress_Should_Fail_When_PhoneNumberIsInvalid()
    {
        var validator = new AddAddressCommandValidator();
        var result = validator.Validate(new AddAddressCommand(Guid.NewGuid(), "user@zionshop.local", "Nguyen Van A", "123456", "TP.HCM", "Phuong Ben Nghe", "123 Main St", false));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AddAddress_Should_Fail_When_WardIsMissing()
    {
        var validator = new AddAddressCommandValidator();
        var result = validator.Validate(new AddAddressCommand(Guid.NewGuid(), "user@zionshop.local", "Nguyen Van A", "0901234567", "TP.HCM", "", "123 Main St", false));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AddAddress_Should_Succeed_When_PayloadValid()
    {
        var validator = new AddAddressCommandValidator();
        var result = validator.Validate(new AddAddressCommand(Guid.NewGuid(), "user@zionshop.local", "Nguyen Van A", "0901234567", "TP.HCM", "Phuong Ben Nghe", "123 Main St", false));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateAddress_Should_Fail_When_PhoneNumberIsInvalid()
    {
        var validator = new UpdateAddressCommandValidator();
        var result = validator.Validate(new UpdateAddressCommand(Guid.NewGuid(), Guid.NewGuid(), "Nguyen Van A", "+84-1-234", "TP.HCM", "Phuong Ben Nghe", "123 Main St", false));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateAddress_Should_Fail_When_WardIsMissing()
    {
        var validator = new UpdateAddressCommandValidator();
        var result = validator.Validate(new UpdateAddressCommand(Guid.NewGuid(), Guid.NewGuid(), "Nguyen Van A", "0901234567", "TP.HCM", " ", "123 Main St", false));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateAddress_Should_Succeed_When_PayloadValid()
    {
        var validator = new UpdateAddressCommandValidator();
        var result = validator.Validate(new UpdateAddressCommand(Guid.NewGuid(), Guid.NewGuid(), "Nguyen Van A", "+84901234567", "TP.HCM", "Phuong Ben Nghe", "123 Main St", true));

        result.IsValid.Should().BeTrue();
    }
}
