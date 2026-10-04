using Microsoft.EntityFrameworkCore;
using SwiftRide.TripService.Domain.Entities;
using SwiftRide.TripService.Infrastructure.Configurations;

namespace SwiftRide.TripService.Infrastructure;

public sealed class TripDbContext(DbContextOptions<TripDbContext> options)
    : DbContext(options)
{
    // Đại diện cho bảng Trips trong DB
    public DbSet<Trip> Trips { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Áp dụng file cấu hình TripConfiguration
        modelBuilder.ApplyConfiguration(new TripConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
