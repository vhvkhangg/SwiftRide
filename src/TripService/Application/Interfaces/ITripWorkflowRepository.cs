using SwiftRide.TripService.Domain.Entities;

namespace SwiftRide.TripService.Application.Interfaces;

public interface ITripWorkflowRepository
{
    Task<IReadOnlyList<Guid>> GetInvitedDriverIdsAsync(Guid tripId, CancellationToken ct);
    Task<DriverInvitation?> GetActiveInvitationAsync(Guid tripId, Guid driverId, DateTime nowUtc, CancellationToken ct);
    Task<bool> AddInvitationAsync(DriverInvitation invitation, CancellationToken ct);
    Task<bool> ResolveInvitationAsync(Guid tripId, Guid driverId, int attempt, DateTime nowUtc, bool timeout, CancellationToken ct);
    Task<IReadOnlyList<DriverInvitation>> GetExpiredInvitationsAsync(DateTime nowUtc, int limit, CancellationToken ct);
}
