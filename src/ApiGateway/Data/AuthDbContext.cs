using Microsoft.EntityFrameworkCore;

namespace SwiftRide.ApiGateway.Data;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Rider";
}

public sealed class RefreshToken
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public Guid FamilyId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByHash { get; set; }
}

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var user = model.Entity<User>();
        user.ToTable("Users");
        user.HasKey(x => x.Id);
        user.Property(x => x.Email).HasMaxLength(254).IsRequired();
        user.Property(x => x.NormalizedEmail).HasMaxLength(254).IsRequired();
        user.HasIndex(x => x.NormalizedEmail).IsUnique();
        user.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        user.Property(x => x.Role).HasMaxLength(64).IsRequired();

        var token = model.Entity<RefreshToken>();
        token.ToTable("RefreshTokens");
        token.HasKey(x => x.Id);
        token.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        token.Property(x => x.ReplacedByHash).HasMaxLength(64).IsFixedLength();
        token.HasIndex(x => x.TokenHash).IsUnique();
        token.HasIndex(x => x.FamilyId);
        token.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
