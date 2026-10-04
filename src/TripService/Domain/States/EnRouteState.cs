using System;
using SwiftRide.TripService.Domain.Enums;

namespace SwiftRide.TripService.Domain.States;

public sealed class EnRouteState : TripState
{
    public override TripStatus Status => TripStatus.EnRoute;

    public override TripState Pickup()
    {
        return new PickedUpState();
    }

    public override TripState Cancel()
    {
        return new CancelledState();
    }

    public override TripState AcceptByDriver(Guid driverId) => throw new InvalidOperationException("Chuyến đi đã có người nhận.");
    public override TripState StartEnRoute() => throw new InvalidOperationException("Đang trên đường rồi.");
    public override TripState Dropoff() => throw new InvalidOperationException("Chưa đón khách.");
    public override TripState MarkPaymentPending() => throw new InvalidOperationException("Chuyến đi chưa kết thúc.");
    public override TripState MarkPaid() => throw new InvalidOperationException("Chuyến đi chưa kết thúc.");
    public override TripState MarkFailed() => throw new InvalidOperationException("Chuyến đi chưa kết thúc.");
}
