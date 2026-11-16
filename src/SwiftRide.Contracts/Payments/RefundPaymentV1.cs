namespace SwiftRide.Contracts.Payments;

// Command bù trừ của Saga: hoàn tiền một payment đã thành công.
public class RefundPaymentV1
{
    public Guid CorrelationId { get; set; }
    public Guid TripId { get; set; }
    public Guid PaymentId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
}
