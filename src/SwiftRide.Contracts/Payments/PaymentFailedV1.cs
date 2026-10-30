namespace SwiftRide.Contracts.Payments;

public class PaymentFailedV1
{
    public Guid CorrelationId { get; set; }

    public Guid TripId { get; set; }

    public Guid PaymentId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "VND";

    public string FailureCode { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; }
}