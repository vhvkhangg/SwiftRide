namespace SwiftRide.Contracts.Matching;

public sealed class ReleaseDriverV1
{
    public Guid TripId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid DriverId { get; set; }
}
