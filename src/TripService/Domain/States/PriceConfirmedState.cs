using System;
using SwiftRide.TripService.Domain.Enums;

namespace SwiftRide.TripService.Domain.States;

public sealed class PriceConfirmedState : TripState
{
    public override TripStatus Status => TripStatus.PriceConfirmed;

    public override TripState ConfirmPrice() => this;

    public override TripState AcceptByDriver(Guid driverId) => new DriverAcceptedState();

    public override TripState Cancel() => new CancelledState();

    public override TripState StartEnRoute() => throw new InvalidOperationException("Tài xế chưa nhận chuyến.");
    public override TripState Pickup() => throw new InvalidOperationException("Tài xế chưa đến điểm đón.");
    public override TripState Dropoff() => throw new InvalidOperationException("Chưa đón khách.");
    public override TripState MarkPaymentPending() => throw new InvalidOperationException("Chuyến đi chưa kết thúc.");
    public override TripState MarkPaid() => throw new InvalidOperationException("Chuyến đi chưa kết thúc.");
    public override TripState MarkFailed() => throw new InvalidOperationException("Chuyến đi chưa kết thúc.");
}
