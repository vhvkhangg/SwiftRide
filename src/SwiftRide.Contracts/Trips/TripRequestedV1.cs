namespace SwiftRide.Contracts.Trips;

// Event: một chuyến đi vừa được yêu cầu.
public class TripRequestedV1
{
    public Guid CorrelationId { get; set; }
    public Guid TripId { get; set; }
    public Guid RiderId { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string DropoffAddress { get; set; } = string.Empty;
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
