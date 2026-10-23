using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SwiftRide.ApiGateway.Data;

namespace SwiftRide.ApiGateway.Auth;

public static class DevelopmentAccounts
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        var password = config["DevelopmentAccounts:Password"]
            ?? throw new InvalidOperationException("DevelopmentAccounts:Password required when seed enabled.");
        if (password.Length < 12) throw new InvalidOperationException("Seed password must be at least 12 characters.");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        foreach (var (email, role) in new[] { ("rider.demo@example.com", "Rider"), ("driver.demo@example.com", "Driver"), ("driver.other@example.com", "Driver") })
        {
            // Never modify an existing user's password or grant a role based on email.
            if (await db.Users.AnyAsync(x => x.NormalizedEmail == email.ToUpperInvariant())) continue;
            var user = new User { Email = email, NormalizedEmail = email.ToUpperInvariant(), Role = role };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
        }
        await db.SaveChangesAsync();
    }
}
