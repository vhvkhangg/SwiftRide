using Microsoft.EntityFrameworkCore;

namespace SwiftRide.TripService.Infrastructure;

public sealed class TripDbContext(DbContextOptions<TripDbContext> options)
    : DbContext(options)
{
    // DbSet<TEntity> declarations belong here when the Trip domain model is implemented.
}
