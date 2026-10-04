using System;
using SwiftRide.TripService.Domain.Enums;

namespace SwiftRide.TripService.Domain.States;

public sealed class DriverAcceptedState : TripState
{
    public override TripStatus Status => TripStatus.DriverAccepted;

    // Các hành động hợp lệ ở bước này
    public override TripState StartEnRoute()
    {
        return new EnRouteState();
    }

    public override TripState Cancel()
    {
        return new CancelledState();
    }

    // Các hành động BỊ CẤM
    public override TripState AcceptByDriver(Guid driverId) => throw new InvalidOperationException("Chuyến đi đã có người nhận.");
    public override TripState Pickup() => throw new InvalidOperationException("Tài xế chưa bắt đầu di chuyển đến điểm đón.");
    public override TripState Dropoff() => throw new InvalidOperationException("Chưa đón khách.");
    public override TripState MarkPaymentPending() => throw new InvalidOperationException("Chuyến đi chưa kết thúc.");
    public override TripState MarkPaid() => throw new InvalidOperationException("Chuyến đi chưa kết thúc.");
    public override TripState MarkFailed() => throw new InvalidOperationException("Chuyến đi chưa kết thúc.");
}
