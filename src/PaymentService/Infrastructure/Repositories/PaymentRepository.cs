using Microsoft.EntityFrameworkCore;
using Npgsql;
using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Domain.Exceptions;
using SwiftRide.PaymentService.Domain.Repositories;
using SwiftRide.PaymentService.Infrastructure.Persistence;

namespace SwiftRide.PaymentService.Infrastructure.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _db;
    public PaymentRepository(PaymentDbContext db) => _db = db;

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Payments.SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<Payment?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default) =>
        _db.Payments.SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);

    public async Task<IReadOnlyList<LedgerEntry>> GetLedgerEntriesByPaymentIdAsync(
        Guid id, CancellationToken ct = default) =>
        await _db.LedgerEntries.Where(x => x.PaymentId == id)
            .OrderBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(Payment payment, CancellationToken ct = default) =>
        await _db.Payments.AddAsync(payment, ct);

    public Task AddLedgerEntriesAsync(IEnumerable<LedgerEntry> entries,
        CancellationToken ct = default) => _db.LedgerEntries.AddRangeAsync(entries, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException pg &&
                  pg.SqlState == PostgresErrorCodes.UniqueViolation &&
                  pg.ConstraintName == "ux_payments_idempotency_key")
        {
            _db.ChangeTracker.Clear();
            throw new DuplicateIdempotencyKeyException(exception);
        }
    }
}
