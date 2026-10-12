using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;

namespace SwiftRide.ApiGateway.Proxy;

public static class IdentityHeaders
{
    public static readonly string[] Names = ["X-User-Id", "X-User-Email", "X-User-Roles"];

    public static ValueTask Enrich(RequestTransformContext context)
    {
        // Always strip on ALL proxy routes, including anonymous routes.
        foreach (var name in Names)
        {
            context.ProxyRequest.Headers.Remove(name);
            context.ProxyRequest.Content?.Headers.Remove(name);
        }

        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            Set(context, Names[0], user.FindFirstValue("sub"));
            Set(context, Names[1], user.FindFirstValue("email"));
            Set(context, Names[2], string.Join(",", user.FindAll("role").Select(x => x.Value)));
        }
        // Downstream relies on gateway identity headers, so don't forward credentials.
        context.ProxyRequest.Headers.Remove("Authorization");
        context.ProxyRequest.Headers.Remove("Cookie");
        return ValueTask.CompletedTask;
    }

    private static void Set(RequestTransformContext context, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            context.ProxyRequest.Headers.Add(name, value);
    }
}
