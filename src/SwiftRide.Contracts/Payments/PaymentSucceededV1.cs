namespace SwiftRide.Contracts.Payments;

public class PaymentSucceededV1
{
    public Guid CorrelationId { get; set; }
    public Guid TripId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTimeOffset OccurredAtUtc { get; set; }
}
