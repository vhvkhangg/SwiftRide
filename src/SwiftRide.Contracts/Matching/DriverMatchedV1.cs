namespace SwiftRide.Contracts.Matching;

// Event: đã tìm được tài xế cho chuyến đi.
public class DriverMatchedV1
{
    public Guid CorrelationId { get; set; }
    public Guid TripId { get; set; }
    public Guid DriverId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
