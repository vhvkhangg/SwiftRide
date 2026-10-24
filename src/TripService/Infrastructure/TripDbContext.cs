using MassTransit;
using Microsoft.EntityFrameworkCore;
using SwiftRide.TripService.Domain.Entities;
using SwiftRide.TripService.Infrastructure.Configurations;

namespace SwiftRide.TripService.Infrastructure;

public sealed class TripDbContext(DbContextOptions<TripDbContext> options)
    : DbContext(options)
{
    // Đại diện cho bảng Trips trong DB
    public DbSet<Trip> Trips { get; set; } = null!;

    public DbSet<DriverInvitation> DriverInvitations => Set<DriverInvitation>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Áp dụng file cấu hình TripConfiguration
        modelBuilder.ApplyConfiguration(new TripConfiguration());
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.Entity<Trip>().Property<uint>("Version").IsRowVersion();
        var invitation = modelBuilder.Entity<DriverInvitation>();
        invitation.HasKey(x => new { x.TripId, x.DriverId });
        invitation.HasIndex(x => new { x.TripId, x.Attempt });
        invitation.HasOne(x => x.Trip).WithMany().HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Cascade);
        base.OnModelCreating(modelBuilder);
    }
}
