using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SwiftRide.PaymentService.Domain.Entities;

namespace SwiftRide.PaymentService.Domain.Repositories
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default
        );

        Task<Payment?> GetByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken = default
        );

        Task AddAsync(
            Payment payment,
            CancellationToken cancellationToken = default
        );

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default
        );
    }
}