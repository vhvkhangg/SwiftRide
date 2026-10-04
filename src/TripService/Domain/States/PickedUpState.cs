using System;
using SwiftRide.TripService.Domain.Enums;

namespace SwiftRide.TripService.Domain.States;

public sealed class PickedUpState : TripState
{
    public override TripStatus Status => TripStatus.PickedUp;

    public override TripState Dropoff()
    {
        return new DroppedOffState();
    }

    // Khi đã đón khách, thông thường không cho phép hủy chuyến một cách đơn giản nữa (có thể phải gọi CSKH)
    // Hoặc nếu hệ thống cho phép, ta sẽ implement sau. Ở đây tạm cấm.
    public override TripState Cancel() => throw new InvalidOperationException("Đã đón khách, không thể tự hủy chuyến.");

    public override TripState AcceptByDriver(Guid driverId) => throw new InvalidOperationException("Chuyến đi đang diễn ra.");
    public override TripState StartEnRoute() => throw new InvalidOperationException("Chuyến đi đang diễn ra.");
    public override TripState Pickup() => throw new InvalidOperationException("Đã đón khách rồi.");
    public override TripState MarkPaymentPending() => throw new InvalidOperationException("Chưa trả khách.");
    public override TripState MarkPaid() => throw new InvalidOperationException("Chưa trả khách.");
    public override TripState MarkFailed() => throw new InvalidOperationException("Chuyến đi đang diễn ra.");
}
