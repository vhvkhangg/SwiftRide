namespace SwiftRide.MatchingService.Application.Services;

/// <summary>
/// Mục "Pricing" trong config/matching-settings.json (tự nạp lại khi đổi, không cần restart).
/// Công thức: p = ((CostPerKm·d + CostPerMinute·t)·surge + Toll)·η·(1 + VatRate).
/// Giá của một chuyến được chốt lúc tạo chuyến; đổi tham số chỉ áp dụng cho báo giá/chuyến mới.
/// </summary>
public sealed class PricingOptions
{
    public decimal CostPerKm { get; set; } = 12m;
    public decimal CostPerMinute { get; set; } = 2m;
    public decimal VatRate { get; set; } = 0.1m;
    public decimal Toll { get; set; } = 0m;

    public void Validate()
    {
        if (CostPerKm < 0 || CostPerMinute < 0 || Toll < 0 || VatRate < 0 || VatRate > 1)
            throw new InvalidOperationException("Cấu hình Pricing không hợp lệ.");
    }
}

/// <summary>Mục "DriverAvailability": vị trí GPS cũ hơn TTL thì tài xế không được ghép.</summary>
public sealed class DriverAvailabilityOptions
{
    public int LocationTtlSeconds { get; set; } = 600;

    public TimeSpan LocationTtl =>
        LocationTtlSeconds is >= 10 and <= 86_400
            ? TimeSpan.FromSeconds(LocationTtlSeconds)
            : throw new InvalidOperationException("DriverAvailability:LocationTtlSeconds phải trong 10..86400.");
}
