using Microsoft.EntityFrameworkCore;
using ZIONShop.Auth.Application.Features.ResetPassword;
using ZIONShop.Auth.Application.Services;
using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Domain.Enums;
using ZIONShop.Auth.Infrastructure.Persistence;
using ZIONShop.Auth.Infrastructure.Repositories;

namespace ZIONShop.Auth.Tests.Features;

public class ResetPasswordCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_ResetPasswordConfirmEmailAndRevokeRefreshTokens_When_CodeIsValid()
    {
        var db = BuildDbContext();
        var hasher = new OtpHasher();
        var user = User.Register("user@zionshop.local", BCrypt.Net.BCrypt.HashPassword("OldPassword@123"), "User");
        var token = RefreshToken.Create(user.Id, "refresh-hash", DateTime.UtcNow.AddDays(1));
        var otp = AuthOtp.Create(user.Id, user.Email, AuthOtpPurpose.PasswordReset, hasher.Hash("123456"), DateTime.UtcNow.AddMinutes(10));
        await db.Users.AddAsync(user);
        await db.RefreshTokens.AddAsync(token);
        await db.AuthOtps.AddAsync(otp);
        await db.SaveChangesAsync();

        var handler = new ResetPasswordCommandHandler(
            new UserRepository(db),
            new AuthOtpRepository(db),
            db,
            hasher);

        var result = await handler.Handle(new ResetPasswordCommand(user.Email, "123456", "NewPassword@123"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await db.Users.Include(u => u.RefreshTokens).SingleAsync(u => u.Id == user.Id);
        updated.EmailConfirmed.Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("NewPassword@123", updated.PasswordHash).Should().BeTrue();
        updated.RefreshTokens.Should().OnlyContain(t => t.IsRevoked);
        (await db.AuthOtps.SingleAsync(o => o.Id == otp.Id)).UsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidResetCode_When_CodeIsWrong()
    {
        var db = BuildDbContext();
        var hasher = new OtpHasher();
        var user = User.Register("user@zionshop.local", BCrypt.Net.BCrypt.HashPassword("OldPassword@123"), "User");
        await db.Users.AddAsync(user);
        await db.AuthOtps.AddAsync(AuthOtp.Create(user.Id, user.Email, AuthOtpPurpose.PasswordReset, hasher.Hash("123456"), DateTime.UtcNow.AddMinutes(10)));
        await db.SaveChangesAsync();

        var handler = new ResetPasswordCommandHandler(
            new UserRepository(db),
            new AuthOtpRepository(db),
            db,
            hasher);

        var result = await handler.Handle(new ResetPasswordCommand(user.Email, "000000", "NewPassword@123"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidResetCode");
    }

    private static AuthDbContext BuildDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AuthDbContext(options);
    }
}
