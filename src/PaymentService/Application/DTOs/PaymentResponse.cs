using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using SwiftRide.PaymentService.Domain.Entities;

namespace SwiftRide.PaymentService.Application.DTOs
{
    public sealed record PaymentResponse(
        Guid Id,
        Guid TripId,
        decimal Amount,
        string Status,
        string IdempotencyKey,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt
    )
    {
        public static PaymentResponse From(Payment payment)
        {
            return new PaymentResponse(
                payment.Id,
                payment.TripId,
                payment.Amount,
                payment.Status.ToString(),
                payment.IdempotencyKey,
                payment.CreatedAt,
                payment.UpdatedAt
            );
        }
    }
}