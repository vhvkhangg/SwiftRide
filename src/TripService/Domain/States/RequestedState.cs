using System;
using SwiftRide.TripService.Domain.Enums;

namespace SwiftRide.TripService.Domain.States;

public sealed class RequestedState : TripState
{
    public override TripStatus Status => TripStatus.Requested;

    public override TripState AcceptByDriver(Guid driverId)
    {
        return new DriverAcceptedState();
    }

    public override TripState Cancel()
    {
        return new CancelledState();
    }

    // Các hành động không hợp lệ khi đang ở trạng thái Requested
    public override TripState StartEnRoute() => throw new InvalidOperationException("Tài xế chưa nhận chuyến.");
    public override TripState Pickup() => throw new InvalidOperationException("Chưa bắt đầu đi đón.");
    public override TripState Dropoff() => throw new InvalidOperationException("Chưa đón khách.");
    public override TripState MarkPaymentPending() => throw new InvalidOperationException("Chuyến đi chưa kết thúc.");
    public override TripState MarkPaid() => throw new InvalidOperationException("Chuyến đi chưa kết thúc.");
    public override TripState MarkFailed() => throw new InvalidOperationException("Chuyến đi chưa kết thúc.");
}
