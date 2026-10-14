namespace SwiftRide.Contracts.Payments;

// Command: yêu cầu PaymentService xử lý thanh toán.
public class ProcessPaymentV1
{
    public Guid CorrelationId { get; set; }
    public Guid TripId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string IdempotencyKey { get; set; } = string.Empty;
    public string PayerAccount { get; set; } = string.Empty;
    public string PayeeAccount { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
}
