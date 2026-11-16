namespace SwiftRide.Contracts.Payments;

// Event: payment đã được hoàn tiền (ledger đảo ngược đã ghi).
public class PaymentRefundedV1
{
    public Guid CorrelationId { get; set; }
    public Guid TripId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
