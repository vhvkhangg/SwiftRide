using SwiftRide.MatchingService.Domain.Models;

namespace SwiftRide.MatchingService.Domain.Interfaces;

public interface IMatchingOutboxRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid tripId, int attempt, CancellationToken cancellationToken);
    Task<bool> EnqueueNoDriverFoundAsync(
        Guid tripId,
        int attempt,
        string reason,
        CancellationToken cancellationToken);
    Task<MatchingOutboxMessage?> ClaimNextAsync(
        Guid lockId,
        DateTime nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);
    Task MarkDeliveredAsync(
        string messageId,
        Guid lockId,
        DateTime deliveredAtUtc,
        CancellationToken cancellationToken);
    Task ScheduleRetryAsync(
        string messageId,
        Guid lockId,
        DateTime nextAttemptAtUtc,
        string error,
        CancellationToken cancellationToken);
}
