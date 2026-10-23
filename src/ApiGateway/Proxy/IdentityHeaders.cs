using System.Security.Claims;

namespace SwiftRide.ApiGateway.Proxy;

// Ocelot caches transport handlers: read the current context per request,
// never capture a user or scoped DbContext in this handler.
public sealed class IdentityHeaders(IHttpContextAccessor accessor) : DelegatingHandler
{
    public static readonly string[] Names = ["X-User-Id", "X-User-Email", "X-User-Roles"];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        foreach (var name in Names)
        {
            request.Headers.Remove(name);
            request.Content?.Headers.Remove(name);
        }

        var user = accessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            Set(request, Names[0], user.FindFirstValue("sub"));
            Set(request, Names[1], user.FindFirstValue("email"));
            Set(request, Names[2], string.Join(",", user.FindAll("role").Select(x => x.Value)));
        }
        // Preserve bearer token: downstream validates JWT independently.
        request.Headers.Remove("Cookie");
        return base.SendAsync(request, ct);
    }

    private static void Set(HttpRequestMessage request, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            request.Headers.Add(name, value);
    }
}
