namespace SwiftRide.TripService.Application.Sagas;

/// <summary>Cấu hình mục "TripPayment".</summary>
public sealed class TripPaymentSagaOptions
{
    /// <summary>Thời gian tối đa chờ kết quả thanh toán sau khi trả khách.</summary>
    public int TimeoutSeconds { get; set; } = 600;

    /// <summary>Chu kỳ worker quét các saga quá hạn.</summary>
    public int ScanIntervalSeconds { get; set; } = 10;
}
