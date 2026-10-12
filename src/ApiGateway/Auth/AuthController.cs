using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SwiftRide.ApiGateway.Data;

namespace SwiftRide.ApiGateway.Auth;

public sealed record Credentials(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password);
public sealed record RefreshRequest([Required, StringLength(128, MinimumLength = 80)] string RefreshToken);

[ApiController]
[Route("auth")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public sealed class AuthController(AuthDbContext db, TokenService tokens,
    IPasswordHasher<User> hasher, DummyPasswordHash dummyHash, TimeProvider clock) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(Credentials request, CancellationToken ct)
    {
        var user = new User { Email = request.Email.Trim(), NormalizedEmail = request.Email.Trim().ToUpperInvariant() };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            return Problem(statusCode: 409, title: "Email already registered.");
        }
        return StatusCode(201, new { user.Id, user.Email });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(Credentials request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.NormalizedEmail == request.Email.Trim().ToUpperInvariant(), ct);
        // Perform a password hash verification even for unknown users to reduce timing differences.
        var candidate = user ?? new User();
        var hash = user?.PasswordHash ?? dummyHash.Value;
        var result = hasher.VerifyHashedPassword(candidate, hash, request.Password);
        if (user is null || result == PasswordVerificationResult.Failed)
            return Problem(statusCode: 401, title: "Invalid credentials.");
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, request.Password);
        var issued = tokens.Issue(user);
        db.RefreshTokens.Add(issued.Entity);
        await db.SaveChangesAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(issued.Response);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var old = await LockToken(request.RefreshToken, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if (old is null) return InvalidToken();
        if (old.RevokedAtUtc is not null)
        {
            // Locking reads see the latest committed version. Replaying a rotated token
            // revokes every active descendant, including one created by a concurrent request.
            await RevokeFamily(old.FamilyId, now, ct);
            await transaction.CommitAsync(ct);
            return InvalidToken();
        }
        if (old.ExpiresAtUtc <= now) return InvalidToken();
        var user = await db.Users.SingleAsync(x => x.Id == old.UserId, ct);
        var issued = tokens.Issue(user, old.FamilyId);
        old.RevokedAtUtc = now;
        old.ReplacedByHash = issued.Entity.TokenHash;
        db.RefreshTokens.Add(issued.Entity);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(issued.Response);
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(RefreshRequest request, CancellationToken ct)
    {
        // Possession of the secret refresh token authorizes logout, even after JWT expiry.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var token = await LockToken(request.RefreshToken, ct);
        if (token is not null)
            await RevokeFamily(token.FamilyId, clock.GetUtcNow().UtcDateTime, ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    private async Task<RefreshToken?> LockToken(string raw, CancellationToken ct)
    {
        var hash = TokenService.Hash(raw);
        // Materialize before SingleOrDefault: FOR UPDATE must remain at the end of SQL.
        var rows = await db.RefreshTokens
            .FromSqlInterpolated($"SELECT * FROM RefreshTokens WHERE TokenHash = {hash} FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    private Task<int> RevokeFamily(Guid family, DateTime now, CancellationToken ct) =>
        db.RefreshTokens.Where(x => x.FamilyId == family && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, (DateTime?)now), ct);

    private ObjectResult InvalidToken() => Problem(statusCode: 401, title: "Invalid refresh token.");


}

public sealed class DummyPasswordHash
{
    public string Value { get; }

    public DummyPasswordHash(Microsoft.Extensions.Options.IOptions<PasswordHasherOptions> options)
    {
        Value = new PasswordHasher<User>(options).HashPassword(new User(),
            Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
    }
}