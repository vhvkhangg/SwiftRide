using SwiftRide.PaymentService.Domain.Enums;

namespace SwiftRide.PaymentService.Domain.Entities;

public class Payment
{
    public Guid Id { get; private set; }
    public Guid TripId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string PayerAccount { get; private set; } = string.Empty;
    public string PayeeAccount { get; private set; } = string.Empty;
    public bool IsTripSynced { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Payment() { }

    private Payment(Guid tripId, decimal amount, string key, string payer, string payee)
    {
        Id = Guid.NewGuid();
        TripId = tripId;
        Amount = amount;
        IdempotencyKey = key;
        PayerAccount = payer;
        PayeeAccount = payee;
        Status = PaymentStatus.Pending;
        IsTripSynced = false;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static Payment Create(Guid tripId, decimal amount, string idempotencyKey,
        string payerAccount, string payeeAccount)
    {
        if (tripId == Guid.Empty)
            throw new ArgumentException("ID chuyến đi không được để trống.", nameof(tripId));
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount),
                "Số tiền phải lớn hơn 0 và có tối đa hai chữ số thập phân.");
        ValidateText(idempotencyKey, nameof(idempotencyKey));
        ValidateText(payerAccount, nameof(payerAccount));
        ValidateText(payeeAccount, nameof(payeeAccount));
        return new Payment(tripId, amount, idempotencyKey, payerAccount, payeeAccount);
    }

    private static void ValidateText(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            throw new ArgumentException($"{parameter} phải có từ 1 đến 128 ký tự.", parameter);
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

    public void MarkTripSynced()
    {
        EnsureStatus(PaymentStatus.Succeeded);
        if (IsTripSynced) return;
        IsTripSynced = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureStatus(PaymentStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException(
                $"Giao dịch phải ở trạng thái '{expected}', trạng thái hiện tại '{Status}'.");
    }
}
