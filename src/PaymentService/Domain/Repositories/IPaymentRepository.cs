using SwiftRide.PaymentService.Domain.Entities;

namespace SwiftRide.PaymentService.Domain.Repositories
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<Payment?> GetSettledByTripIdAsync(
            Guid tripId,
            CancellationToken cancellationToken = default);

        Task<Payment?> GetByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<LedgerEntry>> GetLedgerEntriesByPaymentIdAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            Payment payment,
            CancellationToken cancellationToken = default);

        Task AddLedgerEntriesAsync(
            IEnumerable<LedgerEntry> entries,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default);
    }
}