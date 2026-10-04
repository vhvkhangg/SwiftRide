using System;
using SwiftRide.TripService.Domain.Enums;

namespace SwiftRide.TripService.Domain.States;

public sealed class PaymentPendingState : TripState
{
    public override TripStatus Status => TripStatus.PaymentPending;

    public override TripState MarkPaid()
    {
        return new PaidState();
    }

    public override TripState MarkFailed()
    {
        return new FailedState();
    }

    public override TripState AcceptByDriver(Guid driverId) => throw new InvalidOperationException("Đang chờ thanh toán.");
    public override TripState StartEnRoute() => throw new InvalidOperationException("Đang chờ thanh toán.");
    public override TripState Pickup() => throw new InvalidOperationException("Đang chờ thanh toán.");
    public override TripState Dropoff() => throw new InvalidOperationException("Đã trả khách rồi.");
    public override TripState Cancel() => throw new InvalidOperationException("Không thể hủy khi đang chờ thanh toán.");
    public override TripState MarkPaymentPending() => throw new InvalidOperationException("Đang ở trạng thái chờ thanh toán rồi.");
}
