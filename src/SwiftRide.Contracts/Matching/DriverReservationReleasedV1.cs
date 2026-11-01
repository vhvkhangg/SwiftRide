namespace SwiftRide.Contracts.Matching;

public sealed class DriverReservationReleasedV1
{
    public Guid CorrelationId { get; set; }
    public Guid TripId { get; set; }
    public Guid DriverId { get; set; }
    public int Attempt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
}
