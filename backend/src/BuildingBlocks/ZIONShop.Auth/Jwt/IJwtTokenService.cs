namespace ZIONShop.Auth.Jwt;

public interface IJwtTokenService
{
    JwtTokenResult GenerateAccessToken(Guid userId, string email, IEnumerable<string> roles);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}

public record JwtTokenResult(string Token, DateTime ExpiresAt);
