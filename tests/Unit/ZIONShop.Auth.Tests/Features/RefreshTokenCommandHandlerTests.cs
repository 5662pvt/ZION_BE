using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZIONShop.Auth.Application.Features.RefreshToken;
using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Infrastructure.Persistence;
using ZIONShop.Auth.Infrastructure.Repositories;
using ZIONShop.Auth.Jwt;

namespace ZIONShop.Auth.Tests.Features;

public class RefreshTokenCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_RevokeOldRefreshTokenAndIssueNewOne_When_TokenIsActive()
    {
        var db = BuildDbContext();
        var jwt = new DeterministicJwtTokenService("new-refresh");
        var user = User.Register("user@zionshop.local", BCrypt.Net.BCrypt.HashPassword("Password@123"), "User");
        user.ConfirmEmail();
        var oldHash = jwt.HashRefreshToken("old-refresh");
        await db.Users.AddAsync(user);
        await db.RefreshTokens.AddAsync(RefreshToken.Create(user.Id, oldHash, DateTime.UtcNow.AddDays(1)));
        await db.SaveChangesAsync();

        var users = new UserRepository(db);
        var handler = new RefreshTokenCommandHandler(
            users,
            db,
            jwt,
            Options.Create(new JwtOptions { RefreshDays = 7 }));

        var result = await handler.Handle(new RefreshTokenCommand("old-refresh"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RefreshToken.Should().Be("new-refresh");

        var tokens = await db.RefreshTokens.OrderBy(t => t.CreatedAt).ToListAsync();
        tokens.Should().HaveCount(2);
        tokens.Single(t => t.TokenHash == oldHash).IsRevoked.Should().BeTrue();
        tokens.Single(t => t.TokenHash == jwt.HashRefreshToken("new-refresh")).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidRefreshToken_When_TokenAlreadyRevoked()
    {
        var db = BuildDbContext();
        var jwt = new DeterministicJwtTokenService("new-refresh");
        var user = User.Register("user@zionshop.local", BCrypt.Net.BCrypt.HashPassword("Password@123"), "User");
        var revoked = RefreshToken.Create(user.Id, jwt.HashRefreshToken("old-refresh"), DateTime.UtcNow.AddDays(1));
        revoked.Revoke();
        await db.Users.AddAsync(user);
        await db.RefreshTokens.AddAsync(revoked);
        await db.SaveChangesAsync();

        var handler = new RefreshTokenCommandHandler(
            new UserRepository(db),
            db,
            jwt,
            Options.Create(new JwtOptions { RefreshDays = 7 }));

        var result = await handler.Handle(new RefreshTokenCommand("old-refresh"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidRefreshToken");
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
        private readonly string _refreshToken;

        public DeterministicJwtTokenService(string refreshToken) => _refreshToken = refreshToken;

        public JwtTokenResult GenerateAccessToken(Guid userId, string email, IEnumerable<string> roles) =>
            new($"access-token-for-{userId}", DateTime.UtcNow.AddMinutes(15));

        public string GenerateRefreshToken() => _refreshToken;

        public string HashRefreshToken(string refreshToken) => $"hash:{refreshToken}";
    }
}
