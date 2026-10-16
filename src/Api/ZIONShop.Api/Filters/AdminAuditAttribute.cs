using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ZIONShop.Auth.Authorization;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.Common.Api;
using ZIONShop.Contracts.Admin;

namespace ZIONShop.Api.Filters;

/// <summary>
/// Records one <see cref="AuditLogEntry"/> for a successful (2xx) admin/staff mutation. Resource id is taken
/// from the named route/action-argument key (default "id"), or — for creates, which have no id in the route —
/// reflected off the action result's <c>Data.Id</c>. Writing never fails the request: any exception from
/// <see cref="IAuditLogWriter"/> is caught and logged instead of propagating.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AdminAuditAttribute : Attribute, IAsyncActionFilter
{
    public AdminAuditAttribute(string action, string resourceType)
    {
        Action = action;
        ResourceType = resourceType;
    }

    public string Action { get; }
    public string ResourceType { get; }

    /// <summary>Action-argument key to read the resource id from. Ignored when <see cref="ResourceIdFromResult"/> is set.</summary>
    public string RouteKey { get; init; } = "id";

    /// <summary>Set for creates: there is no id in the route yet, so read it off the result's Data.Id instead.</summary>
    public bool ResourceIdFromResult { get; init; }

    /// <summary>
    /// Set only for endpoints shared by customers and staff (e.g. order cancel): skips logging unless the
    /// caller actually is Staff/Admin, since the controller's own [Authorize] can't express that distinction.
    /// </summary>
    public bool RequireStaffOrAdmin { get; init; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executedContext = await next();
        if (executedContext.Exception is not null) return;

        var statusCode = (executedContext.Result as ObjectResult)?.StatusCode
            ?? (executedContext.Result as StatusCodeResult)?.StatusCode;
        if (statusCode is null || statusCode < 200 || statusCode >= 300) return;

        var httpContext = executedContext.HttpContext;
        var currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUserService>();

        if (RequireStaffOrAdmin && !(currentUser.Roles.Contains(Roles.Staff) || currentUser.Roles.Contains(Roles.Admin)))
            return;

        var writer = httpContext.RequestServices.GetService<IAuditLogWriter>();
        if (writer is null) return;

        var resourceId = ResourceIdFromResult
            ? ExtractIdFromResult(executedContext.Result)
            : (context.ActionArguments.TryGetValue(RouteKey, out var value) ? value?.ToString() : null);

        var entry = new AuditLogEntry(
            DateTime.UtcNow,
            currentUser.UserId,
            currentUser.Email,
            Action,
            ResourceType,
            resourceId,
            httpContext.Request.Method,
            httpContext.Request.Path.Value ?? httpContext.Request.Path.ToString(),
            statusCode.Value,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            Activity.Current?.GetTagItem("correlation.id") as string,
            Activity.Current?.TraceId.ToString());

        try
        {
            await writer.WriteAsync(entry, CancellationToken.None);
        }
        catch (Exception ex)
        {
            httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("AdminAudit")
                .LogError(ex, "Failed to write admin audit log for action {Action}", Action);
        }
    }

    // ApiResponse<T> hides the base ApiResponse.Data (object?) with its own `new T? Data` of a different
    // type, so the default Type.GetProperty("Data") throws AmbiguousMatchException between the two —
    // DeclaredOnly resolves to the most-derived (ApiResponse<T>) declaration instead.
    private static string? ExtractIdFromResult(IActionResult? result)
    {
        if (result is not ObjectResult { Value: ApiResponse response }) return null;
        var data = response.GetType()
            .GetProperty("Data", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            ?.GetValue(response);
        return data?.GetType().GetProperty("Id")?.GetValue(data)?.ToString();
    }
}
