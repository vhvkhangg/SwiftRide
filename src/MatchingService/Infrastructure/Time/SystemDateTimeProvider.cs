using SwiftRide.MatchingService.Domain.Interfaces;

namespace SwiftRide.MatchingService.Infrastructure.Time;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
