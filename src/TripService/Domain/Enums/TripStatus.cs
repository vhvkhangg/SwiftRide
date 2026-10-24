namespace SwiftRide.TripService.Domain.Enums;

public enum TripStatus
{
    Requested,
    PriceConfirmed,
    DriverAccepted,
    EnRoute,
    PickedUp,
    DroppedOff,
    PaymentPending,
    Paid,
    Cancelled,
    Failed
}
