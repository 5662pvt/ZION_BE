using ZIONShop.Products.Application.Options;

namespace ZIONShop.Api.Middleware;

/// <summary>Skips /swagger* and the uploaded-images static path -- Swagger UI needs inline
/// scripts/styles the CSP below would block, and the FE loads product images cross-origin.</summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var skip = path.StartsWithSegments("/swagger") ||
                   path.StartsWithSegments($"/{ImageStorageOptions.UploadsFolderName}");

        if (!skip)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
                return Task.CompletedTask;
            });
        }

        return _next(context);
    }
}
