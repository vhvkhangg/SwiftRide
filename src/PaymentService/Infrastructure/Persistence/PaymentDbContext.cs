using Microsoft.EntityFrameworkCore;
using SwiftRide.PaymentService.Domain.Entities;

namespace SwiftRide.PaymentService.Infrastructure;

public sealed class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PaymentDbContext).Assembly
        );
    }
}
