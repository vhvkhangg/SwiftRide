namespace SwiftRide.TripService.Domain.Entities;

public sealed class DriverInvitation
{
    public Trip Trip { get; set; } = null!;
    public Guid TripId { get; set; }
    public Guid DriverId { get; set; }
    public int Attempt { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsResolved { get; set; }
}
