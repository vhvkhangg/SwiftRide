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
        var internalKey = Key(config, "ServiceAuth:SecretKey");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // User tokens come from Keycloak (RS256, keys from JWKS); never from a shared secret.
                KeycloakTokens.Configure(options, config);
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
                options.TokenValidationParameters = InternalParameters(internalKey);
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

    private static TokenValidationParameters InternalParameters(byte[] key) => new()
    {
        ValidateIssuer = true, ValidIssuer = "SwiftRide.Services",
        ValidateAudience = true, ValidAudience = "SwiftRide.Internal",
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        RoleClaimType = "role", NameClaimType = "sub", ClockSkew = TimeSpan.FromSeconds(30)
    };
}

public static class KeycloakTokens
{
    /// <summary>Validates Keycloak access tokens: issuer, audience, RS256 signature from JWKS, lifetime.</summary>
    public static void Configure(JwtBearerOptions options, IConfiguration config)
    {
        var authority = config["Authentication:Authority"]
            ?? throw new InvalidOperationException("Authentication:Authority (Keycloak realm URL) required.");
        var audience = config["Authentication:Audience"]
            ?? throw new InvalidOperationException("Authentication:Audience required.");
        options.Authority = authority;
        var metadata = config["Authentication:MetadataAddress"];
        if (!string.IsNullOrWhiteSpace(metadata)) options.MetadataAddress = metadata;
        options.RequireHttpsMetadata = config.GetValue("Authentication:RequireHttpsMetadata", true);
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = authority,
            ValidateAudience = true, ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RoleClaimType = "role", NameClaimType = "sub", ClockSkew = TimeSpan.FromSeconds(30)
        };
    }
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
