using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace ZIONShop.Logging.Middleware;

public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing) && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString("N");

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
                context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // The ASP.NET Core instrumentation already started the request's root Activity by the time this
        // middleware runs, so tagging it here (rather than only via an instrumentation Enrich callback)
        // reliably covers both cases -- caller-supplied header and server-generated fallback -- with one
        // write, and makes correlation.id queryable on the span itself, not just in logs.
        Activity.Current?.SetTag("correlation.id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
