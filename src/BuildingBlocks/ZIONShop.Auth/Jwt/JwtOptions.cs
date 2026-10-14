namespace ZIONShop.Auth.Jwt;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "ZIONShop";
    public string Audience { get; set; } = "ZIONShop.Client";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessMinutes { get; set; } = 15;
    public int RefreshDays { get; set; } = 7;
}
