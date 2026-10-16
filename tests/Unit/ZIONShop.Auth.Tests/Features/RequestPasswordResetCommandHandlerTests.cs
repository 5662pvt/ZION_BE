using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.Auth.Application.Features.ForgotPassword;
using ZIONShop.Auth.Application.Services;
using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Infrastructure.Persistence;
using ZIONShop.Auth.Infrastructure.Repositories;
using ZIONShop.Common.Email;

namespace ZIONShop.Auth.Tests.Features;

public class RequestPasswordResetCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnGenericSuccess_When_EmailDoesNotExist()
    {
        var db = BuildDbContext();
        var otpDelivery = new AuthOtpDeliveryService(new AuthOtpRepository(db), new ThrowingEmailSender(), new OtpHasher());
        var handler = new RequestPasswordResetCommandHandler(new UserRepository(db), db, otpDelivery, NullLogger<RequestPasswordResetCommandHandler>.Instance);

        var result = await handler.Handle(new RequestPasswordResetCommand("unknown@zionshop.local"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnGenericSuccess_NotEmailDeliveryFailed_When_AccountExistsButEmailSendingThrows()
    {
        var db = BuildDbContext();
        var user = User.Register("user@zionshop.local", BCrypt.Net.BCrypt.HashPassword("Password@123"), "User");
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        var otpDelivery = new AuthOtpDeliveryService(new AuthOtpRepository(db), new ThrowingEmailSender(), new OtpHasher());
        var handler = new RequestPasswordResetCommandHandler(new UserRepository(db), db, otpDelivery, NullLogger<RequestPasswordResetCommandHandler>.Instance);

        var result = await handler.Handle(new RequestPasswordResetCommand(user.Email), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().Be("If an account exists for this email, a reset code has been sent.");
    }

    private static AuthDbContext BuildDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AuthDbContext(options);
    }

    private sealed class ThrowingEmailSender : IEmailSender
    {
        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("SMTP is down");
    }
}
