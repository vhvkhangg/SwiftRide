using Microsoft.EntityFrameworkCore;
using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Domain.Repositories;
using SwiftRide.PaymentService.Infrastructure.Persistence;

namespace SwiftRide.PaymentService.Infrastructure.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _dbContext;

    public PaymentRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Payment?> GetByIdAsync(
    Guid id,
    CancellationToken cancellationToken = default)
    {
        return await _dbContext.Payments
            .SingleOrDefaultAsync(
                payment => payment.Id == id,
                cancellationToken);
    }

    public async Task<Payment?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default
    )
    {
        return await _dbContext.Payments
            .SingleOrDefaultAsync(
                payment => payment.IdempotencyKey == idempotencyKey,
                cancellationToken
            );
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetLedgerEntriesByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default
    )
    {
        return await _dbContext.LedgerEntries
            .Where(entry => entry.PaymentId == paymentId)
            .OrderBy(entry => entry.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        Payment payment,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Payments.AddAsync(
            payment,
            cancellationToken);
    }

    public async Task AddLedgerEntriesAsync(
        IEnumerable<LedgerEntry> entries,
        CancellationToken cancellationToken = default
    )
    {
        await _dbContext.LedgerEntries.AddRangeAsync(
            entries,
            cancellationToken
        );
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}