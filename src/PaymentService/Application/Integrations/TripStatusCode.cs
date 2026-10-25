
namespace SwiftRide.PaymentService.Application.Integrations;

public enum TripStatusCode
{
    Requested = 0,
    PriceConfirmed = 1,
    DriverAccepted = 2,
    EnRoute = 3,
    PickedUp = 4,
    DroppedOff = 5,
    PaymentPending = 6,
    Paid = 7,
    Cancelled = 8,
    Failed = 9
}
