using MassTransit;
using SwiftRide.Contracts.Trips;
using SwiftRide.MatchingService.Application.DTOs;
using SwiftRide.MatchingService.Application.Interfaces;

namespace SwiftRide.MatchingService.Application.Consumers;

public class TripRequestedConsumer : IConsumer<TripRequestedV1>
{
    private readonly IMatchingService _matching;
    private readonly ILogger<TripRequestedConsumer> _logger;

    public TripRequestedConsumer(IMatchingService matching, ILogger<TripRequestedConsumer> logger)
    {
        _matching = matching;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TripRequestedV1> context)
    {
        var message = context.Message;
        if (message.TripId == Guid.Empty ||
            !message.PickupLatitude.HasValue || !message.PickupLongitude.HasValue ||
            !double.IsFinite(message.PickupLatitude.Value) ||
            !double.IsFinite(message.PickupLongitude.Value) ||
            message.PickupLatitude.Value < -90 || message.PickupLatitude.Value > 90 ||
            message.PickupLongitude.Value < -180 || message.PickupLongitude.Value > 180)
        {
            // ArgumentException không retry; MassTransit chuyển message vào queue _error.
            throw new ArgumentException("TripRequestedV1 cần TripId và tọa độ điểm đón hợp lệ.");
        }

        // ponytail: dùng bán kính 5 km như API /match; thêm cấu hình khi cần thay đổi theo khu vực.
        var request = new MatchRequest(message.TripId.ToString(),
            message.PickupLatitude.Value, message.PickupLongitude.Value, 5);
        var result = await _matching.MatchAsync(request, context.CancellationToken);

        // ponytail: chỉ tìm ứng viên và ghi log; thêm event kết quả khi chốt luồng chọn/nhận chuyến.
        _logger.LogInformation("Đã nhận chuyến {TripId}, tìm thấy {DriverCount} tài xế gần điểm đón",
            message.TripId, result.Drivers.Count);
    }
}
