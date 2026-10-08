namespace SwiftRide.PaymentService.Application.Integrations;

public enum TripStatusCode
{
    Requested = 0,
    DriverAccepted = 1,
    EnRoute = 2,
    PickedUp = 3,
    DroppedOff = 4,
    PaymentPending = 5,
    Paid = 6,
    Cancelled = 7,
    Failed = 8
}