namespace SwiftRide.MatchingService.Domain.Interfaces;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
