using System.Net;
using System.Security.Claims;

namespace OrderService;

// Register before UseAuthorization. Allow only the actual gateway peer address.
// Behind a sidecar/proxy use mTLS or enforce this trust boundary at the network layer.
public sealed class GatewayIdentityMiddleware(RequestDelegate next, IPAddress gatewayAddress)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var peer = context.Connection.RemoteIpAddress?.MapToIPv6();
        if (peer is null || !peer.Equals(gatewayAddress.MapToIPv6()))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var ids = context.Request.Headers["X-User-Id"];
        var emails = context.Request.Headers["X-User-Email"];
        var roles = context.Request.Headers["X-User-Roles"];
        if (ids.Count != 1 || !Guid.TryParse(ids[0], out var id) ||
            emails.Count != 1 || string.IsNullOrWhiteSpace(emails[0]) ||
            roles.Count != 1 || string.IsNullOrWhiteSpace(roles[0]))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(ClaimTypes.Email, emails[0]!)
        };
        foreach (var role in roles[0]!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            claims.Add(new Claim(ClaimTypes.Role, role));
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TrustedGateway"));
        await next(context);
    }
}

In OrderService Program.cs:
builder.Services.AddAuthorization();
app.UseRouting();
app.UseMiddleware<GatewayIdentityMiddleware>(IPAddress.Parse("10.0.0.10"));
app.UseAuthorization();
app.MapGet("/api/orders/me", (HttpContext ctx) =>
    Results.Ok(new { UserId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) }))
    .RequireAuthorization();
app.MapGet("/api/orders/admin", () => Results.Ok())
    .RequireAuthorization(policy => policy.RequireRole("Admin"));
