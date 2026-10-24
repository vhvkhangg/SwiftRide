namespace SwiftRide.Contracts.Matching;

public sealed class FindDriverForTripV1
{
    public Guid CorrelationId { get; set; }
    public Guid TripId { get; set; }
    public int Attempt { get; set; }
    public double PickupLatitude { get; set; }
    public double PickupLongitude { get; set; }
    public double RadiusKm { get; set; } = 5;
    public IReadOnlyCollection<Guid> ExcludedDriverIds { get; set; } = [];
    public DateTimeOffset OccurredAtUtc { get; set; }
}
