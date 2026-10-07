using System;
using SwiftRide.TripService.Domain.Enums;

namespace SwiftRide.TripService.Domain.States;

public abstract class TripState
{
    public abstract TripStatus Status { get; }

    // Các hành động có thể xảy ra trong hệ thống
    public abstract TripState AcceptByDriver(Guid driverId);
    public abstract TripState StartEnRoute();
    public abstract TripState Pickup();
    public abstract TripState Dropoff();
    public abstract TripState Cancel();
    public abstract TripState MarkPaymentPending();
    public abstract TripState MarkPaid();
    public abstract TripState MarkFailed();
}
