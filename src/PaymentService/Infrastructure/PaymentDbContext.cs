using Microsoft.EntityFrameworkCore;

namespace SwiftRide.PaymentService.Infrastructure;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options)
    : DbContext(options)
{
    // DbSet<TEntity> declarations belong here when the Payment domain model is implemented.
}
