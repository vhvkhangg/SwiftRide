namespace SwiftRide.TripService.Application.Services;

public sealed class TripMatchingOptions
{
    public int MaxAttempts { get; set; } = 3;
    public int InvitationTimeoutSeconds { get; set; } = 120;
    public double SearchRadiusKm { get; set; } = 5;
}
