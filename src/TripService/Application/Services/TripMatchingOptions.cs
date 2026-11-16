namespace SwiftRide.TripService.Application.Services;

/// <summary>
/// Mục "TripMatching". Nơi duy nhất quyết định ngưỡng từ chối k: file config/trip-matching.json
/// (mount vào container, tự nạp lại khi đổi, không cần restart). Biến môi trường vẫn ghi đè được (dùng cho test).
/// </summary>
public sealed class TripMatchingOptions
{
    /// <summary>
    /// k: số lần tài xế được phép từ chối (kể cả lời mời hết giờ). Quy ước: chuyến chỉ Failed khi
    /// DeclineCount &gt; k. Mặc định 2 ⇒ thất bại ở lần từ chối thứ 3.
    /// </summary>
    public int DeclineThreshold { get; set; } = 2;
    public int InvitationTimeoutSeconds { get; set; } = 120;
    public double SearchRadiusKm { get; set; } = 5;

    public void Validate()
    {
        if (DeclineThreshold < 0 || DeclineThreshold > 50 || InvitationTimeoutSeconds < 1 ||
            !double.IsFinite(SearchRadiusKm) || SearchRadiusKm <= 0)
            throw new InvalidOperationException("Cấu hình TripMatching không hợp lệ.");
    }
}
