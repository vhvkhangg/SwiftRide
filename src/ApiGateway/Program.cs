using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SwiftRide.ApiGateway.Auth;
using SwiftRide.ApiGateway.Data;
using SwiftRide.ApiGateway.Proxy;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: false).AddEnvironmentVariables();

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<JwtSettings>()
    .BindConfiguration("JwtSettings")
    .ValidateDataAnnotations()
    .Validate(x => Encoding.UTF8.GetByteCount(x.SecretKey) >= 32, "JWT key must contain at least 32 UTF-8 bytes.")
    .ValidateOnStart();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<DummyPasswordHash>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.Configure<PasswordHasherOptions>(x => x.IterationCount = 210_000);
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AuthDb")
        ?? throw new InvalidOperationException("ConnectionStrings:AuthDb is required.")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<TokenService>((options, tokens) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = tokens.ValidationParameters;
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.WebSockets.IsWebSocketRequest &&
                    context.Request.Path == "/api/hubs/trips")
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var userId) || userId == Guid.Empty)
                    context.Fail("Invalid subject.");
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser()
        .RequireClaim("sub").RequireClaim("email"));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddOcelot(builder.Configuration)
    .AddDelegatingHandler<IdentityHeaders>(global: true);
var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapWhen(context => context.Request.Path.StartsWithSegments("/auth") ||
    context.Request.Path.StartsWithSegments("/health"), local =>
{
    local.UseRouting();
    local.UseAuthentication();
    local.UseAuthorization();
    local.UseRateLimiter();
    local.UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();
        endpoints.MapGet("/health", () => Results.Ok(new { service = "api-gateway", status = "ok" }))
            .AllowAnonymous();
    });
});
// Development bootstrap only. Production uses reviewed EF migrations.
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("AuthDatabase:EnsureCreated"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.EnsureCreatedAsync();
}
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("DevelopmentAccounts:Enabled"))
    await DevelopmentAccounts.SeedAsync(app.Services, builder.Configuration);
app.UseWebSockets();
app.UseAuthentication();
app.UseWhen(context => context.Request.Path.StartsWithSegments("/api/hubs/trips"), hub =>
{
    hub.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        if (!context.User.IsInRole("Rider") && !context.User.IsInRole("Driver"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        foreach (var header in IdentityHeaders.Names) context.Request.Headers.Remove(header);
        context.Request.Headers.Remove("Cookie");
        await next(context);
    });
});
await app.UseOcelot();
await app.RunAsync();

public partial class Program { }
