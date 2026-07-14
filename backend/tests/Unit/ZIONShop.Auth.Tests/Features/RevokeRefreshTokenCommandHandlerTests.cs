using Microsoft.EntityFrameworkCore;
using ZIONShop.Auth.Application.Features.RevokeToken;
using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Infrastructure.Persistence;
using ZIONShop.Auth.Infrastructure.Repositories;
using ZIONShop.Auth.Jwt;

namespace ZIONShop.Auth.Tests.Features;

public class RevokeRefreshTokenCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_RevokeRefreshToken_When_TokenExists()
    {
        var db = BuildDbContext();
        var jwt = new DeterministicJwtTokenService();
        var user = User.Register("user@zionshop.local", BCrypt.Net.BCrypt.HashPassword("Password@123"), "User");
        var hash = jwt.HashRefreshToken("refresh-token");
        await db.Users.AddAsync(user);
        await db.RefreshTokens.AddAsync(RefreshToken.Create(user.Id, hash, DateTime.UtcNow.AddDays(1)));
        await db.SaveChangesAsync();

        var handler = new RevokeRefreshTokenCommandHandler(new UserRepository(db), db, jwt);

        var result = await handler.Handle(new RevokeRefreshTokenCommand("refresh-token"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.RefreshTokens.SingleAsync(t => t.TokenHash == hash)).IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_When_TokenDoesNotExist()
    {
        var db = BuildDbContext();
        var handler = new RevokeRefreshTokenCommandHandler(new UserRepository(db), db, new DeterministicJwtTokenService());

        var result = await handler.Handle(new RevokeRefreshTokenCommand("missing-token"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    private static AuthDbContext BuildDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AuthDbContext(options);
    }

    private sealed class DeterministicJwtTokenService : IJwtTokenService
    {
        public JwtTokenResult GenerateAccessToken(Guid userId, string email, IEnumerable<string> roles) =>
            new($"access-token-for-{userId}", DateTime.UtcNow.AddMinutes(15));

        public string GenerateRefreshToken() => "refresh-token";

        public string HashRefreshToken(string refreshToken) => $"hash:{refreshToken}";
    }
}
