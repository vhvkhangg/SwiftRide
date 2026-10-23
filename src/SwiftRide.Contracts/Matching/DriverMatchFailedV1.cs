namespace SwiftRide.Contracts.Matching;

public class DriverMatchFailedV1
{
    public Guid CorrelationId { get; set; }
    public Guid TripId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
}
