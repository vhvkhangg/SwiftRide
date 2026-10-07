using System;
using SwiftRide.TripService.Domain.Enums;

namespace SwiftRide.TripService.Domain.States;

public sealed class DroppedOffState : TripState
{
    public override TripStatus Status => TripStatus.DroppedOff;

    public override TripState MarkPaymentPending()
    {
        return new PaymentPendingState();
    }

    public override TripState AcceptByDriver(Guid driverId) => throw new InvalidOperationException("Chuyến đi đã hoàn thành chặng đường.");
    public override TripState StartEnRoute() => throw new InvalidOperationException("Chuyến đi đã hoàn thành chặng đường.");
    public override TripState Pickup() => throw new InvalidOperationException("Chuyến đi đã hoàn thành chặng đường.");
    public override TripState Dropoff() => throw new InvalidOperationException("Đã trả khách rồi.");
    public override TripState Cancel() => throw new InvalidOperationException("Không thể hủy khi đã đi đến đích.");
    public override TripState MarkPaid() => throw new InvalidOperationException("Chưa gửi yêu cầu thanh toán.");
    public override TripState MarkFailed() => throw new InvalidOperationException("Chưa gửi yêu cầu thanh toán.");
}
