namespace SwiftRide.Contracts.Matching;

public sealed class DriverFoundForTripV1
{
    public Guid CorrelationId { get; set; }
    public Guid TripId { get; set; }
    public int Attempt { get; set; }
    public Guid DriverId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
