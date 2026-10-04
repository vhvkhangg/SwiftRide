using System;
using SwiftRide.TripService.Domain.Enums;

namespace SwiftRide.TripService.Domain.States;

public sealed class FailedState : TripState
{
    public override TripStatus Status => TripStatus.Failed;

    // Terminal State - Mọi hành động tiếp theo đều bị cấm
    public override TripState AcceptByDriver(Guid driverId) => throw new InvalidOperationException("Chuyến đi đã thất bại.");
    public override TripState StartEnRoute() => throw new InvalidOperationException("Chuyến đi đã thất bại.");
    public override TripState Pickup() => throw new InvalidOperationException("Chuyến đi đã thất bại.");
    public override TripState Dropoff() => throw new InvalidOperationException("Chuyến đi đã thất bại.");
    public override TripState Cancel() => throw new InvalidOperationException("Chuyến đi đã thất bại.");
    public override TripState MarkPaymentPending() => throw new InvalidOperationException("Chuyến đi đã thất bại.");
    public override TripState MarkPaid() => throw new InvalidOperationException("Chuyến đi đã thất bại.");
    public override TripState MarkFailed() => throw new InvalidOperationException("Chuyến đi đã thất bại.");
}
