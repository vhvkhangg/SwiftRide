using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SwiftRide.PaymentService.Domain.Enums;

namespace SwiftRide.PaymentService.Domain.Entities
{
    public class Payment
    {
        public Guid Id { get; private set; }

        public Guid TripId { get; private set; }

        public decimal Amount { get; private set; }

        public PaymentStatus Status { get; private set; }

        public string IdempotencyKey { get; private set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        private Payment() { }

        private Payment(
            Guid id,
            Guid tripId,
            decimal amount,
            string idempotencyKey,
            DateTimeOffset createdAt)
        {
            Id = id;
            TripId = tripId;
            Amount = amount;
            IdempotencyKey = idempotencyKey;
            Status = PaymentStatus.Pending;
            CreatedAt = createdAt;
            UpdatedAt = createdAt;
        }

        public static Payment Create(
            Guid tripId,
            decimal amount,
            string idempotencyKey
        )
        {
            if (tripId == Guid.Empty)
            {
                throw new ArgumentException(
                    "ID chuyến đi không được để trống.",
                    nameof(tripId)
                );
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    "Số tiền thanh toán phải lớn hơn 0."
                );
            }

            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                throw new ArgumentException(
                    "Khoá chống trùng lặp giao dịch không được để trống.",
                    nameof(idempotencyKey)
                );
            }

            var now = DateTimeOffset.UtcNow;

            return new Payment(
                Guid.NewGuid(),
                tripId,
                amount,
                idempotencyKey,
                now
            );
        }

        public void MarkSucceeded()
        {
            EnsureStatus(PaymentStatus.Pending);

            Status = PaymentStatus.Succeeded;
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        public void MarkFailed()
        {
            EnsureStatus(PaymentStatus.Pending);

            Status = PaymentStatus.Failed;
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        public void Refund()
        {
            EnsureStatus(PaymentStatus.Succeeded);

            Status = PaymentStatus.Refunded;
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        private void EnsureStatus(PaymentStatus expectedStatus)
        {
            if (Status != expectedStatus)
            {
                throw new InvalidOperationException(
                    $"Giao dịch phải ở trạng thái '{expectedStatus}', " +
                    $"nhưng trạng thái hiện tại là '{Status}'."
                );
            }
        }
    }
}