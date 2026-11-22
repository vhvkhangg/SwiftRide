using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SwiftRide.ApiGateway.Registration;
using SwiftRide.ApiGateway.Proxy;
using SwiftRide.Security;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddHttpClient("keycloak", client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure(options =>
    {
        // Keycloak is the only token issuer; Gateway validates before proxying, services validate again.
        KeycloakTokens.Configure(options, builder.Configuration);
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
// Default policy = authenticated user; every proxied route uses it (AuthorizationPolicy "default").
builder.Services.AddAuthorization();
// Browser clients (Expo web preview of the Rider/Driver apps) call the Gateway cross-origin.
// Native apps do not send Origin, so CORS only matters for the web build; origins come from config.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(corsOrigins)
    .WithHeaders("Authorization", "Content-Type")
    .WithMethods("GET", "POST")
    .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
// Rider self sign-up from the app: Gateway creates the account in Keycloak with a service account
// (manage-users only). Anonymous, so it is rate limited per client IP.
var registration = builder.Configuration.GetSection(RegistrationOptions.Section).Get<RegistrationOptions>() ?? new();
builder.Services.AddSingleton(registration);
builder.Services.AddHttpClient<KeycloakRegistrationClient>(client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RegistrationEndpoint.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("Registration:PermitsPerWindow", 10), Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
});
// YARP: routes/clusters in appsettings.json section "ReverseProxy"; Compose overrides cluster addresses.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context => context.AddRequestTransform(IdentityHeaders.ApplyAsync));
var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapWhen(context => context.Request.Path.StartsWithSegments("/health"), local =>
{
    local.UseRouting();
    local.UseEndpoints(endpoints =>
    {
        endpoints.MapGet("/health", () => Results.Ok(new { service = "api-gateway", status = "ok" }));
        // Ready only when Keycloak discovery (issuer + JWKS location) is reachable.
        endpoints.MapGet("/health/ready", async (IHttpClientFactory clients, IConfiguration config, CancellationToken ct) =>
        {
            var metadata = config["Authentication:MetadataAddress"]
                ?? config["Authentication:Authority"]!.TrimEnd('/') + "/.well-known/openid-configuration";
            try
            {
                using var response = await clients.CreateClient("keycloak").GetAsync(metadata, ct);
                return response.IsSuccessStatusCode
                    ? Results.Ok(new { service = "api-gateway", status = "ready" })
                    : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        });
    });
});
app.UseWebSockets();
app.UseRouting();
// Before authentication so preflight (OPTIONS, no token) is answered here instead of returning 401.
app.UseCors();
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
        await next(context);
    });
});
app.UseAuthorization();
app.UseRateLimiter();
app.MapPost("/api/auth/register", async (RegisterRiderRequest request, KeycloakRegistrationClient keycloak, CancellationToken ct) =>
{
    var errors = RiderRegistration.Validate(request);
    if (errors.Count > 0) return Results.ValidationProblem(errors, title: "Thông tin đăng ký chưa hợp lệ");
    return RegistrationEndpoint.ToHttpResult(await keycloak.CreateRiderAsync(request, ct), request.Email!.Trim());
}).AllowAnonymous().RequireRateLimiting(RegistrationEndpoint.RateLimitPolicy);
app.MapReverseProxy();
await app.RunAsync();

public partial class Program { }
