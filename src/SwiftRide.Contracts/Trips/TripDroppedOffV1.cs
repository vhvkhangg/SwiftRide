namespace SwiftRide.Contracts.Trips;

// Event: tài xế đã trả khách; mở đầu Saga thanh toán (TripPaymentStateMachine).
public class TripDroppedOffV1
{
    public Guid CorrelationId { get; set; }
    public Guid TripId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string IdempotencyKey { get; set; } = string.Empty;
    public string PayerAccount { get; set; } = string.Empty;
    public string PayeeAccount { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "Wallet";
    public DateTimeOffset OccurredAtUtc { get; set; }
}
