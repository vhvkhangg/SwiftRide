using System.Security.Claims;
using SwiftRide.Security;

// Include src/Shared/ServiceSecurity.cs and reference JwtBearer, as the real services do.
// Configure JwtSettings and ServiceAuth via secrets/environment before running.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddServiceSecurity(builder.Configuration);
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/api/orders/me", (HttpContext context) =>
    Results.Ok(new { userId = context.User.UserId(), roles = context.User.FindAll("role").Select(x => x.Value) }))
    .RequireAuthorization(policy => policy.RequireRole("Rider"));
// Never derive authorization from X-User-* headers, even when accessed behind Gateway.
app.Run();
