using SwiftRide.PaymentService.Domain.Entities;

namespace SwiftRide.PaymentService.Application.DTOs;

public sealed record PaymentResponse(
    Guid Id,
    Guid TripId,
    decimal Amount,
    string Status,
    string IdempotencyKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsTripSynced)
{
    public static PaymentResponse From(Payment p) => new(
        p.Id, p.TripId, p.Amount, p.Status.ToString(), p.IdempotencyKey,
        p.CreatedAt, p.UpdatedAt, p.IsTripSynced);
}
