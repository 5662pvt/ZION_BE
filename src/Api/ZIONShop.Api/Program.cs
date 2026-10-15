using Asp.Versioning;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ZIONShop.Api.Extensions;
using ZIONShop.Api.Health;
using ZIONShop.Api.Middleware;
using ZIONShop.Api.Options;
using ZIONShop.Auth.Application.DependencyInjection;
using ZIONShop.Auth.DependencyInjection;
using ZIONShop.Auth.Infrastructure.DependencyInjection;
using ZIONShop.Auth.Infrastructure.Persistence;
using ZIONShop.Caching.DependencyInjection;
using ZIONShop.Cart.Application.DependencyInjection;
using ZIONShop.Cart.Infrastructure.DependencyInjection;
using ZIONShop.Cart.Infrastructure.Persistence;
using ZIONShop.Common.Api;
using ZIONShop.Common.Behaviors;
using ZIONShop.Common.Email;
using ZIONShop.Common.Exceptions;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.Logging.Middleware;
using ZIONShop.Logging.Serilog;
using ZIONShop.Products.Application.DependencyInjection;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Application.Options;
using ZIONShop.Products.Infrastructure.DependencyInjection;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Users.Application.DependencyInjection;
using ZIONShop.Users.Infrastructure.DependencyInjection;
using ZIONShop.Users.Infrastructure.Persistence;
using ZIONShop.Orders.Application.DependencyInjection;
using ZIONShop.Orders.Infrastructure.DependencyInjection;
using ZIONShop.Inventory.Application.DependencyInjection;
using ZIONShop.Inventory.Infrastructure.DependencyInjection;
using ZIONShop.Payments.Application.DependencyInjection;
using ZIONShop.Payments.Infrastructure.DependencyInjection;
using ZIONShop.Promotions.Application.DependencyInjection;
using ZIONShop.Promotions.Infrastructure.DependencyInjection;
using ZIONShop.Notifications.Application.DependencyInjection;
using ZIONShop.Notifications.Infrastructure.DependencyInjection;
using ZIONShop.Reviews.Application.DependencyInjection;
using ZIONShop.Reviews.Infrastructure.DependencyInjection;
using ZIONShop.Admin.Application.DependencyInjection;
using ZIONShop.Admin.Infrastructure.DependencyInjection;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

builder.UseZionSerilog();

// Controllers + API versioning
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value is not null && kvp.Value.Errors.Count > 0)
            .SelectMany(kvp => kvp.Value!.Errors.Select(e => new ErrorDetail
            {
                Field = kvp.Key.StartsWith("$.") ? kvp.Key[2..] : kvp.Key,
                Message = e.ErrorMessage
            }))
            .ToList();

        return new BadRequestObjectResult(ApiResponse.Fail("Validation failed", errors));
    };
});
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.ReportApiVersions = true;
}).AddApiExplorer(o =>
{
    o.GroupNameFormat = "'v'VVV";
    o.SubstituteApiVersionInUrl = true;
});

// Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ZIONShop API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token only (without 'Bearer ' prefix)."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme }
            },
            Array.Empty<string>()
        }
    });
});

// CORS
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173", "http://localhost:3000" };
builder.Services.AddCors(opts => opts.AddDefaultPolicy(p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("X-Correlation-Id", "Retry-After", "Idempotent-Replayed")));

// Building blocks
builder.Services.AddZionAuth(builder.Configuration);
builder.Services.AddZionEventBus(builder.Configuration);
builder.Services.AddZionEmail(builder.Configuration);
if (!builder.Environment.IsEnvironment("Testing"))
{
    try { builder.Services.AddZionCaching(builder.Configuration); }
    catch { /* Redis optional in local dev */ }
}

// MediatR pipeline behaviors (registered against shared assemblies)
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));

// Phase 1 modules
builder.Services.AddAuthApplication();
builder.Services.AddAuthInfrastructure(builder.Configuration);
builder.Services.AddUsersApplication();
builder.Services.AddUsersInfrastructure(builder.Configuration);
builder.Services.AddProductsApplication();
builder.Services.AddProductsInfrastructure(builder.Configuration);
builder.Services.AddCartApplication();
builder.Services.AddCartInfrastructure(builder.Configuration);

// Skeleton modules (no-op DI extensions, registered for future use)
builder.Services
    .AddOrdersApplication().AddOrdersInfrastructure(builder.Configuration)
    .AddInventoryApplication().AddInventoryInfrastructure(builder.Configuration)
    .AddPaymentsApplication().AddPaymentsInfrastructure(builder.Configuration)
    .AddPromotionsApplication().AddPromotionsInfrastructure(builder.Configuration)
    .AddNotificationsApplication().AddNotificationsInfrastructure(builder.Configuration)
    .AddReviewsApplication().AddReviewsInfrastructure(builder.Configuration)
    .AddAdminApplication().AddAdminInfrastructure(builder.Configuration);

// Global exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres", tags: new[] { "ready" })
    .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" })
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "ready" });

// Rate limiting (auth, checkout, search endpoints). Limits are read from IOptions<RateLimitingOptions>
// inside each partition factory (at request time, after DI/config is fully built) rather than off
// builder.Configuration at this point in top-level code -- top-level reads run before
// WebApplicationFactory-based test hosts get a chance to layer in ConfigureAppConfiguration overrides.
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection(RateLimitingOptions.SectionName));
var rateLimitJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
builder.Services.AddRateLimiter(opts =>
{
    opts.AddPolicy("auth", httpContext =>
    {
        var limits = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter("auth", _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromSeconds(limits.WindowSeconds),
            PermitLimit = limits.AuthPermitLimit,
            QueueLimit = 0
        });
    });

    opts.AddPolicy("checkout", httpContext =>
    {
        var limits = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        var userId = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var key = userId is not null
            ? $"user:{userId}"
            : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromSeconds(limits.WindowSeconds),
            PermitLimit = limits.CheckoutPermitLimit,
            QueueLimit = 0
        });
    });

    opts.AddPolicy("search", httpContext =>
    {
        var limits = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromSeconds(limits.WindowSeconds),
            PermitLimit = limits.SearchPermitLimit,
            QueueLimit = 0
        });
    });

    opts.OnRejected = async (context, token) =>
    {
        var retryAfterSeconds = 60;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            retryAfterSeconds = (int)retryAfter.TotalSeconds;

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
        context.HttpContext.Response.ContentType = "application/json";

        const string message = "Bạn thao tác quá nhanh, vui lòng thử lại sau.";
        var body = ApiResponse.Fail(message, new[] { new ErrorDetail { Code = "RateLimit.Exceeded", Message = message } });
        await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(body, rateLimitJsonOptions), token);
    };
});

var app = builder.Build();

await app.MigrateAndSeedAsync();

app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors();

var uploadsRoot = app.Services.GetRequiredService<IUploadsRootProvider>().RootPath;
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = $"/{ImageStorageOptions.UploadsFolderName}"
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ZIONShop v1"));
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    },
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/healthz", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }));

app.Run();

public partial class Program { }
