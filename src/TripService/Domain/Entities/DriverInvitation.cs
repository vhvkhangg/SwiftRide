namespace SwiftRide.TripService.Domain.Entities;

public sealed class DriverInvitation
{
    public Guid TripId { get; set; }
    public Guid DriverId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
