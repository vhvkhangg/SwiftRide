using SwiftRide.PaymentService.Domain.Entities;

namespace SwiftRide.PaymentService.Application.DTOs
{
    public sealed record RefundPaymentResponse(
        Guid PaymentId,
        Guid TripId,
        decimal Amount,
        string Status,
        DateTimeOffset RefundedAt
    )
    {
        public static RefundPaymentResponse From(Payment payment)
        {
            return new RefundPaymentResponse(
                payment.Id,
                payment.TripId,
                payment.Amount,
                payment.Status.ToString(),
                payment.UpdatedAt
            );
        }
    }
}