namespace ZIONShop.Api.Options;

public static class AuthRateLimitKeyResolver
{
    public static string GetPartitionKey(HttpContext httpContext)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ip}";
    }
}
