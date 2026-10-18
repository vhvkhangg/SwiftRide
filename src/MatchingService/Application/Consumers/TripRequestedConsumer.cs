using MassTransit;
using SwiftRide.Contracts.Matching;
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

        // Mặc định bán kính 5 km như API /match.
        var request = new MatchRequest(
            message.TripId.ToString(),
            message.PickupLatitude.Value,
            message.PickupLongitude.Value,
            5);
        var result = await _matching.MatchAsync(request, context.CancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Đã nhận chuyến {TripId}, tìm thấy {DriverCount} tài xế gần điểm đón",
                message.TripId, result.Drivers.Count);
        }

        // TH1: Tìm thấy tài xế -> Chọn tài xế gần nhất và Publish DriverMatchedV1
        if (result.Drivers.Count > 0)
        {
            var bestDriver = result.Drivers[0];

            if (!Guid.TryParse(bestDriver.DriverId, out var driverGuid))
            {
                driverGuid = Guid.NewGuid();
            }

            await context.Publish(new DriverMatchedV1
            {
                CorrelationId = message.CorrelationId,
                TripId = message.TripId,
                DriverId = driverGuid,
                OccurredAtUtc = DateTimeOffset.UtcNow
            }, context.CancellationToken);

            _logger.LogInformation("Đã bắn event DriverMatchedV1 cho Trip {TripId} với tài xế {DriverId}",
                message.TripId, driverGuid);
        }
        // TH2: Không có tài xế trong bán kính -> Publish DriverMatchFailedV1
        else
        {
            await context.Publish(new DriverMatchFailedV1
            {
                CorrelationId = message.CorrelationId,
                TripId = message.TripId,
                Reason = "Không tìm thấy tài xế khả dụng gần điểm đón.",
                OccurredAtUtc = DateTimeOffset.UtcNow
            }, context.CancellationToken);

            _logger.LogWarning("Không tìm thấy tài xế cho Trip {TripId}, đã bắn DriverMatchFailedV1", message.TripId);
        }
    }
}