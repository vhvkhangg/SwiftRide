using System;
using SwiftRide.TripService.Domain.Enums;

namespace SwiftRide.TripService.Domain.States;

public sealed class CancelledState : TripState
{
    public override TripStatus Status => TripStatus.Cancelled;

    // Terminal State - Mọi hành động tiếp theo đều bị cấm
    public override TripState AcceptByDriver(Guid driverId) => throw new InvalidOperationException("Chuyến đi đã bị hủy.");
    public override TripState StartEnRoute() => throw new InvalidOperationException("Chuyến đi đã bị hủy.");
    public override TripState Pickup() => throw new InvalidOperationException("Chuyến đi đã bị hủy.");
    public override TripState Dropoff() => throw new InvalidOperationException("Chuyến đi đã bị hủy.");
    public override TripState Cancel() => throw new InvalidOperationException("Chuyến đi đã bị hủy.");
    public override TripState MarkPaymentPending() => throw new InvalidOperationException("Chuyến đi đã bị hủy.");
    public override TripState MarkPaid() => throw new InvalidOperationException("Chuyến đi đã bị hủy.");
    public override TripState MarkFailed() => throw new InvalidOperationException("Chuyến đi đã bị hủy.");
}
