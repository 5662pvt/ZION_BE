using Asp.Versioning;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ZIONShop.Api.Extensions;
using ZIONShop.Auth.Application.DependencyInjection;
using ZIONShop.Auth.DependencyInjection;
using ZIONShop.Auth.Infrastructure.DependencyInjection;
using ZIONShop.Auth.Infrastructure.Persistence;
using ZIONShop.Caching.DependencyInjection;
using ZIONShop.Cart.Application.DependencyInjection;
using ZIONShop.Cart.Infrastructure.DependencyInjection;
using ZIONShop.Cart.Infrastructure.Persistence;
using ZIONShop.Common.Behaviors;
using ZIONShop.Common.Exceptions;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.Logging.Middleware;
using ZIONShop.Logging.Serilog;
using ZIONShop.Products.Application.DependencyInjection;
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
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(opts => opts.AddDefaultPolicy(p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("X-Correlation-Id")));

// Building blocks
builder.Services.AddZionAuth(builder.Configuration);
builder.Services.AddZionEventBus(builder.Configuration);
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

// Rate limiting (auth endpoints)
builder.Services.AddRateLimiter(opts =>
{
    opts.AddFixedWindowLimiter("auth", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 20;
        o.QueueLimit = 0;
    });
});

var app = builder.Build();

await app.MigrateAndSeedAsync();

app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ZIONShop v1"));
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/healthz", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }));

app.Run();

public partial class Program { }
