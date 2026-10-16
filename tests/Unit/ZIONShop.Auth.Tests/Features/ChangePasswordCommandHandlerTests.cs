using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZIONShop.Auth.Application.Features.ChangePassword;
using ZIONShop.Auth.Application.Services;
using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.Auth.Infrastructure.Persistence;
using ZIONShop.Auth.Infrastructure.Repositories;
using ZIONShop.Auth.Jwt;

namespace ZIONShop.Auth.Tests.Features;

public class ChangePasswordCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_IssueNewTokensAndRevokeOldRefreshTokens_When_CurrentPasswordIsCorrect()
    {
        var db = BuildDbContext();
        var jwt = new DeterministicJwtTokenService("new-refresh");
        var user = User.Register("user@zionshop.local", BCrypt.Net.BCrypt.HashPassword("OldPassword@123"), "User");
        user.ConfirmEmail();
        var oldToken = RefreshToken.Create(user.Id, jwt.HashRefreshToken("old-refresh"), DateTime.UtcNow.AddDays(1));
        await db.Users.AddAsync(user);
        await db.RefreshTokens.AddAsync(oldToken);
        await db.SaveChangesAsync();

        var users = new TestUserRepository(db);
        var issuer = new AuthTokenIssuer(users, db, jwt, Options.Create(new JwtOptions { RefreshDays = 7 }));
        var handler = new ChangePasswordCommandHandler(users, db, issuer);

        var result = await handler.Handle(new ChangePasswordCommand(user.Id, "OldPassword@123", "NewPassword@123"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RefreshToken.Should().Be("new-refresh");

        var updated = await db.Users.Include(u => u.RefreshTokens).SingleAsync(u => u.Id == user.Id);
        BCrypt.Net.BCrypt.Verify("NewPassword@123", updated.PasswordHash).Should().BeTrue();
        updated.RefreshTokens.Single(t => t.Id == oldToken.Id).IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidCurrentPassword_When_CurrentPasswordIsWrong()
    {
        var db = BuildDbContext();
        var jwt = new DeterministicJwtTokenService("new-refresh");
        var user = User.Register("user@zionshop.local", BCrypt.Net.BCrypt.HashPassword("OldPassword@123"), "User");
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        var users = new UserRepository(db);
        var issuer = new AuthTokenIssuer(users, db, jwt, Options.Create(new JwtOptions { RefreshDays = 7 }));
        var handler = new ChangePasswordCommandHandler(users, db, issuer);

        var result = await handler.Handle(new ChangePasswordCommand(user.Id, "WrongPassword@123", "NewPassword@123"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCurrentPassword");
    }

    [Fact]
    public async Task Handle_Should_ReturnPasswordUnchanged_When_NewPasswordEqualsCurrent()
    {
        var db = BuildDbContext();
        var jwt = new DeterministicJwtTokenService("new-refresh");
        var user = User.Register("user@zionshop.local", BCrypt.Net.BCrypt.HashPassword("SamePassword@123"), "User");
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        var users = new UserRepository(db);
        var issuer = new AuthTokenIssuer(users, db, jwt, Options.Create(new JwtOptions { RefreshDays = 7 }));
        var handler = new ChangePasswordCommandHandler(users, db, issuer);

        var result = await handler.Handle(new ChangePasswordCommand(user.Id, "SamePassword@123", "SamePassword@123"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.PasswordUnchanged");
    }

    [Fact]
    public void Validator_Should_Fail_When_NewPasswordIsWeak()
    {
        var validator = new ChangePasswordCommandValidator();
        var result = validator.Validate(new ChangePasswordCommand(Guid.NewGuid(), "OldPassword@123", "weak"));

        result.IsValid.Should().BeFalse();
    }

    private static AuthDbContext BuildDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AuthDbContext(options);
    }

    /// <summary>
    /// Delegates to the real UserRepository except for SetLastLoginAsync, which EF Core's InMemory
    /// provider cannot translate (ExecuteUpdateAsync is unsupported there; Npgsql in production handles it fine).
    /// </summary>
    private sealed class TestUserRepository : IUserRepository
    {
        private readonly AuthDbContext _db;
        private readonly UserRepository _inner;

        public TestUserRepository(AuthDbContext db)
        {
            _db = db;
            _inner = new UserRepository(db);
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _inner.GetByIdAsync(id, cancellationToken);
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => _inner.GetByEmailAsync(email, cancellationToken);
        public Task<User?> GetByEmailForUpdateAsync(string email, CancellationToken cancellationToken = default) => _inner.GetByEmailForUpdateAsync(email, cancellationToken);
        public Task<User?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) => _inner.GetByRefreshTokenHashAsync(tokenHash, cancellationToken);
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => _inner.EmailExistsAsync(email, cancellationToken);
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => _inner.AddAsync(user, cancellationToken);
        public Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default) => _inner.AddRefreshTokenAsync(token, cancellationToken);
        public IQueryable<User> Query() => _inner.Query();
        public void Update(User user) => _inner.Update(user);

        public Task SetLastLoginAsync(Guid userId, DateTime at, CancellationToken cancellationToken = default)
        {
            var user = _db.ChangeTracker.Entries<User>().Select(e => e.Entity).Single(u => u.Id == userId);
            _db.Entry(user).Property(u => u.LastLoginAt).CurrentValue = at;
            return Task.CompletedTask;
        }
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
