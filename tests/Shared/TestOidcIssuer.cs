using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace SwiftRide.Tests.Shared;

/// <summary>
/// Stand-in for the Keycloak realm in tests: issues RS256 access tokens with the same claims
/// (sub, role, aud) and can serve OIDC discovery + JWKS so real processes validate them.
/// </summary>
public sealed class TestOidcIssuer : IDisposable
{
    public const string Issuer = "http://keycloak.test/realms/swiftride";
    public const string Audience = "swiftride-api";
    private readonly RSA rsa = RSA.Create(2048);
    private readonly RSA foreign = RSA.Create(2048);

    public TestOidcIssuer() => Key = new RsaSecurityKey(rsa) { KeyId = "test-signing-key" };

    public RsaSecurityKey Key { get; }

    public Dictionary<string, string?> Settings(string? metadataAddress = null) => new()
    {
        ["Authentication:Authority"] = Issuer,
        ["Authentication:Audience"] = Audience,
        ["Authentication:RequireHttpsMetadata"] = "false",
        ["Authentication:MetadataAddress"] = metadataAddress
    };

    /// <summary>In-process apps: trust this issuer's key without fetching discovery.</summary>
    public void TrustIn(IServiceCollection services) =>
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
            configuration.SigningKeys.Add(Key);
            options.Configuration = configuration;
        });

    /// <summary>Out-of-process apps (real Gateway): serve discovery and JWKS like Keycloak does.</summary>
    public void MapDiscovery(IEndpointRouteBuilder endpoints, Func<string> baseUrl)
    {
        endpoints.MapGet("/.well-known/openid-configuration", () => Results.Json(new Dictionary<string, object>
        {
            ["issuer"] = Issuer,
            ["jwks_uri"] = baseUrl().TrimEnd('/') + "/jwks",
            ["id_token_signing_alg_values_supported"] = new[] { "RS256" }
        })).AllowAnonymous();
        endpoints.MapGet("/jwks", () =>
        {
            var parameters = rsa.ExportParameters(false);
            return Results.Json(new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "RSA", use = "sig", alg = "RS256", kid = Key.KeyId,
                        n = Base64UrlEncoder.Encode(parameters.Modulus), e = Base64UrlEncoder.Encode(parameters.Exponent)
                    }
                }
            });
        }).AllowAnonymous();
    }

    public string Token(string subject, string role, string? audience = null, bool expired = false,
        string? issuer = null, bool foreignKey = false, string? hmacKey = null)
    {
        var now = DateTime.UtcNow;
        SigningCredentials credentials = hmacKey is not null
            ? new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(hmacKey)), SecurityAlgorithms.HmacSha256)
            : new(foreignKey ? new RsaSecurityKey(foreign) { KeyId = Key.KeyId } : Key, SecurityAlgorithms.RsaSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer ?? Issuer, audience ?? Audience,
            [new Claim("sub", subject), new Claim("role", role)],
            now.AddMinutes(-10), expired ? now.AddMinutes(-1) : now.AddMinutes(5), credentials));
    }

    public void Dispose()
    {
        rsa.Dispose();
        foreign.Dispose();
    }
}
