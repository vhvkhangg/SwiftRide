using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace SwiftRide.Security;

public static class ServiceSecurity
{
    public static IServiceCollection AddServiceSecurity(this IServiceCollection services, IConfiguration config)
    {
        var issuer = config["JwtSettings:Issuer"] ?? throw new InvalidOperationException("JWT issuer required.");
        var audience = config["JwtSettings:Audience"] ?? throw new InvalidOperationException("JWT audience required.");
        var key = Key(config, "JwtSettings:SecretKey");
        var internalKey = Key(config, "ServiceAuth:SecretKey");
        if (key.SequenceEqual(internalKey))
            throw new InvalidOperationException("User and service signing keys must differ.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = Parameters(key, issuer, audience);
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.WebSockets.IsWebSocketRequest &&
                            context.Request.Path == "/hubs/trips")
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        if (!Guid.TryParse(context.Principal?.FindFirstValue("sub"), out var userId) || userId == Guid.Empty)
                            context.Fail("Invalid subject.");
                        return Task.CompletedTask;
                    }
                };
            })
            .AddJwtBearer("Service", options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = Parameters(internalKey, "SwiftRide.Services", "SwiftRide.Internal");
            });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
                .RequireClaim("sub").Build();
            options.AddPolicy("InternalRead", policy => policy.AddAuthenticationSchemes("Service")
                .RequireAuthenticatedUser().RequireClaim("sub", "payment-service", "matching-service"));
            options.AddPolicy("InternalPricing", policy => policy.AddAuthenticationSchemes("Service")
                .RequireAuthenticatedUser().RequireClaim("sub", "trip-service"));
            options.AddPolicy("Payments", policy => policy.AddAuthenticationSchemes("Service")
                .RequireAuthenticatedUser().RequireClaim("sub", "payment-service"));
            options.AddPolicy("Matching", policy => policy.AddAuthenticationSchemes("Service")
                .RequireAuthenticatedUser().RequireClaim("sub", "matching-service"));
        });
        return services;
    }

    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id) && id != Guid.Empty
            ? id : throw new UnauthorizedAccessException("Invalid subject.");

    public static string InternalToken(IConfiguration config, string service)
    {
        var now = DateTime.UtcNow;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "SwiftRide.Services", "SwiftRide.Internal", [new Claim("sub", service)], now, now.AddMinutes(1),
            new SigningCredentials(new SymmetricSecurityKey(Key(config, "ServiceAuth:SecretKey")), SecurityAlgorithms.HmacSha256)));
    }

    private static byte[] Key(IConfiguration config, string name)
    {
        var key = Encoding.UTF8.GetBytes(config[name] ?? "");
        return key.Length >= 32 ? key : throw new InvalidOperationException(name + " must contain at least 32 bytes.");
    }

    private static TokenValidationParameters Parameters(byte[] key, string issuer, string audience) => new()
    {
        ValidateIssuer = true, ValidIssuer = issuer,
        ValidateAudience = true, ValidAudience = audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        RoleClaimType = "role", NameClaimType = "sub", ClockSkew = TimeSpan.FromSeconds(30)
    };
}

public sealed class InternalTripHandler(IConfiguration config) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Authorization = new("Bearer", ServiceSecurity.InternalToken(config,
            config["ServiceAuth:ServiceName"] ?? throw new InvalidOperationException("Service name required.")));
        return base.SendAsync(request, ct);
    }
}
