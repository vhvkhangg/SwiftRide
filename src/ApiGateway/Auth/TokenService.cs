using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SwiftRide.ApiGateway.Data;

namespace SwiftRide.ApiGateway.Auth;

public sealed class JwtSettings
{
    [Required, MinLength(32)] public string SecretKey { get; set; } = "";
    [Required] public string Issuer { get; set; } = "";
    [Required] public string Audience { get; set; } = "";
    [Range(1, 60)] public int ExpiryTime { get; set; } = 15; // minutes
    [Range(1, 90)] public int RefreshTokenDays { get; set; } = 7;
}

public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAtUtc);

public sealed class TokenService(IOptions<JwtSettings> options, TimeProvider clock)
{
    private readonly JwtSettings settings = options.Value;

    public TokenValidationParameters ValidationParameters => new()
    {
        ValidateIssuer = true,
        ValidIssuer = settings.Issuer,
        ValidateAudience = true,
        ValidAudience = settings.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtRegisteredClaimNames.Sub,
        RoleClaimType = "role"
    };

    public ClaimsPrincipal ValidateAccessToken(string token) =>
        new JwtSecurityTokenHandler { MapInboundClaims = false }
            .ValidateToken(token, ValidationParameters, out _);

    public (TokenResponse Response, RefreshToken Entity) Issue(User user, Guid? familyId = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(settings.ExpiryTime);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("role", user.Role)
        };
        var jwt = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now, expires,
            new SigningCredentials(ValidationParameters.IssuerSigningKey, SecurityAlgorithms.HmacSha256));
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (new TokenResponse(new JwtSecurityTokenHandler().WriteToken(jwt), raw, expires),
            new RefreshToken
            {
                UserId = user.Id, TokenHash = Hash(raw), FamilyId = familyId ?? Guid.NewGuid(),
                ExpiresAtUtc = now.AddDays(settings.RefreshTokenDays)
            });
    }

    public static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
