using Microsoft.AspNetCore.Authentication.JwtBearer;
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
app.MapReverseProxy();
await app.RunAsync();

public partial class Program { }
