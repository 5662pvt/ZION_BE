using Microsoft.EntityFrameworkCore;
using ZIONShop.Auth.Application.Features.LockCustomer;
using ZIONShop.Auth.Application.Features.UnlockCustomer;
using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Infrastructure.Persistence;
using ZIONShop.Auth.Infrastructure.Repositories;

namespace ZIONShop.Auth.Tests.Features;

public class LockCustomerCommandHandlerTests
{
    [Fact]
    public async Task Lock_Should_DeactivateUserAndRevokeRefreshTokens_When_CustomerExists()
    {
        var db = BuildDbContext();
        var user = User.Register("customer@zionshop.local", BCrypt.Net.BCrypt.HashPassword("Password@123"), "Customer");
        var token = RefreshToken.Create(user.Id, "refresh-hash", DateTime.UtcNow.AddDays(1));
        await db.Users.AddAsync(user);
        await db.RefreshTokens.AddAsync(token);
        await db.SaveChangesAsync();

        var handler = new LockCustomerCommandHandler(new UserRepository(db), db);

        var result = await handler.Handle(new LockCustomerCommand(user.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();

        var updated = await db.Users.Include(u => u.RefreshTokens).SingleAsync(u => u.Id == user.Id);
        updated.IsActive.Should().BeFalse();
        updated.RefreshTokens.Should().OnlyContain(t => t.IsRevoked);
    }

    [Fact]
    public async Task Lock_Should_ReturnCannotLockSelf_When_ActingUserIsTheTarget()
    {
        var db = BuildDbContext();
        var admin = User.Register("admin@zionshop.local", BCrypt.Net.BCrypt.HashPassword("Password@123"), "Admin", new[] { "Admin" });
        await db.Users.AddAsync(admin);
        await db.SaveChangesAsync();

        var handler = new LockCustomerCommandHandler(new UserRepository(db), db);

        var result = await handler.Handle(new LockCustomerCommand(admin.Id, admin.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Admin.CannotLockSelf");
    }

    [Fact]
    public async Task Lock_Should_ReturnCustomerNotFound_When_UserDoesNotExist()
    {
        var db = BuildDbContext();
        var handler = new LockCustomerCommandHandler(new UserRepository(db), db);

        var result = await handler.Handle(new LockCustomerCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Admin.CustomerNotFound");
    }

    [Fact]
    public async Task Unlock_Should_ActivateUser_When_CustomerExists()
    {
        var db = BuildDbContext();
        var user = User.Register("customer@zionshop.local", BCrypt.Net.BCrypt.HashPassword("Password@123"), "Customer");
        user.Deactivate();
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        var handler = new UnlockCustomerCommandHandler(new UserRepository(db), db);

        var result = await handler.Handle(new UnlockCustomerCommand(user.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();

        var updated = await db.Users.SingleAsync(u => u.Id == user.Id);
        updated.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Unlock_Should_ReturnCustomerNotFound_When_UserDoesNotExist()
    {
        var db = BuildDbContext();
        var handler = new UnlockCustomerCommandHandler(new UserRepository(db), db);

        var result = await handler.Handle(new UnlockCustomerCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Admin.CustomerNotFound");
    }

    private static AuthDbContext BuildDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AuthDbContext(options);
    }
}
