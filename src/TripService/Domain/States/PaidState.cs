using System;
using SwiftRide.TripService.Domain.Enums;

namespace SwiftRide.TripService.Domain.States;

public sealed class PaidState : TripState
{
    public override TripStatus Status => TripStatus.Paid;

    // Terminal State - Mọi hành động tiếp theo đều bị cấm
    public override TripState AcceptByDriver(Guid driverId) => throw new InvalidOperationException("Chuyến đi đã kết thúc.");
    public override TripState StartEnRoute() => throw new InvalidOperationException("Chuyến đi đã kết thúc.");
    public override TripState Pickup() => throw new InvalidOperationException("Chuyến đi đã kết thúc.");
    public override TripState Dropoff() => throw new InvalidOperationException("Chuyến đi đã kết thúc.");
    public override TripState Cancel() => throw new InvalidOperationException("Chuyến đi đã kết thúc.");
    public override TripState MarkPaymentPending() => throw new InvalidOperationException("Chuyến đi đã thanh toán xong.");
    public override TripState MarkPaid() => throw new InvalidOperationException("Chuyến đi đã thanh toán xong.");
    public override TripState MarkFailed() => throw new InvalidOperationException("Chuyến đi đã kết thúc thành công.");
}
