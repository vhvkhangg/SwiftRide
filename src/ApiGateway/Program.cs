using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SwiftRide.ApiGateway.Auth;
using SwiftRide.ApiGateway.Data;
using SwiftRide.ApiGateway.Proxy;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

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
    options.UseMySql(builder.Configuration.GetConnectionString("AuthDb")
        ?? throw new InvalidOperationException("ConnectionStrings:AuthDb is required."),
        new MySqlServerVersion(new Version(8, 4, 0))));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<TokenService>((options, tokens) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = tokens.ValidationParameters;
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
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context => context.AddRequestTransform(IdentityHeaders.Enrich));

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers(); // /auth/* terminates here; it is never proxied.
app.MapGet("/health", () => Results.Ok(new { service = "api-gateway", status = "ok" })).AllowAnonymous();
app.MapReverseProxy();

// Development bootstrap only. Production uses reviewed EF migrations.
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("AuthDatabase:EnsureCreated"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.EnsureCreatedAsync();
}
app.Run();

public partial class Program { }
