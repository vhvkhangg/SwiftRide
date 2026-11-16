using MassTransit;

namespace SwiftRide.TripService.Application.Sagas;

/// <summary>
/// Trạng thái tiến trình của Saga thanh toán (một bản ghi cho mỗi chuyến, CorrelationId = TripId).
/// Trạng thái nghiệp vụ của chuyến vẫn nằm ở entity Trip; saga chỉ giữ tiến trình và quyết định bù trừ.
/// </summary>
public sealed class TripPaymentSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string IdempotencyKey { get; set; } = string.Empty;
    public string PayerAccount { get; set; } = string.Empty;
    public string PayeeAccount { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "Wallet";

    public Guid? PaymentId { get; set; }
    public int CommandsSent { get; set; }
    public int FaultCount { get; set; }
    public string? FailureReason { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime DeadlineUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Kết quả activity timeout: chuyến đã kịp Paid trước khi saga quá hạn.</summary>
    public bool TripAlreadyPaid { get; set; }

    /// <summary>PostgreSQL xmin, dùng cho optimistic concurrency của saga repository.</summary>
    public uint RowVersion { get; set; }
}
