using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;

namespace SwiftRide.ApiGateway.Proxy;

/// <summary>
/// YARP request transform: never forward identity headers or cookies chosen by the client.
/// Re-adds X-User-* from the validated JWT for observability only; services authorize from
/// the forwarded Authorization header, never from these headers.
/// </summary>
public static class IdentityHeaders
{
    public static readonly string[] Names = ["X-User-Id", "X-User-Email", "X-User-Roles"];

    public static ValueTask ApplyAsync(RequestTransformContext context)
    {
        var request = context.ProxyRequest;
        foreach (var name in Names)
        {
            request.Headers.Remove(name);
            request.Content?.Headers.Remove(name);
        }
        request.Headers.Remove("Cookie");

        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            Set(request, Names[0], user.FindFirstValue("sub"));
            Set(request, Names[1], user.FindFirstValue("email"));
            Set(request, Names[2], string.Join(",", user.FindAll("role").Select(x => x.Value)));
        }
        return ValueTask.CompletedTask;
    }

    private static void Set(HttpRequestMessage request, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            request.Headers.TryAddWithoutValidation(name, value);
    }
}
