using Microsoft.EntityFrameworkCore;
using SwiftRide.TripService.Application.Interfaces;
using SwiftRide.TripService.Domain.Entities;

namespace SwiftRide.TripService.Infrastructure.Repositories;

public sealed class TripWorkflowRepository(TripDbContext db) : ITripWorkflowRepository
{
    public async Task<IReadOnlyList<Guid>> GetInvitedDriverIdsAsync(Guid tripId, CancellationToken ct) =>
        await db.DriverInvitations.AsNoTracking()
            .Where(invitation => invitation.TripId == tripId)
            .Select(invitation => invitation.DriverId)
            .ToListAsync(ct);

    public Task<DriverInvitation?> GetActiveInvitationAsync(
        Guid tripId,
        Guid driverId,
        DateTime nowUtc,
        CancellationToken ct) =>
        db.DriverInvitations.SingleOrDefaultAsync(invitation =>
            invitation.TripId == tripId &&
            invitation.DriverId == driverId &&
            !invitation.IsResolved &&
            invitation.ExpiresAtUtc > nowUtc, ct);

    public async Task<bool> AddInvitationAsync(DriverInvitation invitation, CancellationToken ct)
    {
        var existing = await db.DriverInvitations.FirstOrDefaultAsync(item =>
            item.TripId == invitation.TripId &&
            item.Attempt == invitation.Attempt, ct);
        if (existing is not null)
        {
            if (existing.DriverId != invitation.DriverId)
                throw new InvalidOperationException("Attempt đã có invitation cho Driver khác.");
            return false;
        }

        db.DriverInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ResolveInvitationAsync(
        Guid tripId,
        Guid driverId,
        int attempt,
        DateTime nowUtc,
        bool timeout,
        CancellationToken ct)
    {
        var query = db.DriverInvitations.Where(item =>
            item.TripId == tripId &&
            item.DriverId == driverId &&
            item.Attempt == attempt &&
            !item.IsResolved);
        query = timeout
            ? query.Where(item => item.ExpiresAtUtc <= nowUtc)
            : query.Where(item => item.ExpiresAtUtc > nowUtc);

        return await query.ExecuteUpdateAsync(update => update
            .SetProperty(item => item.IsResolved, true)
            .SetProperty(item => item.ExpiresAtUtc, nowUtc), ct) == 1;
    }

    public async Task<IReadOnlyList<DriverInvitation>> GetExpiredInvitationsAsync(
        DateTime nowUtc,
        int limit,
        CancellationToken ct) =>
        await db.DriverInvitations
            .Where(invitation => invitation.ExpiresAtUtc <= nowUtc &&
                !invitation.IsResolved &&
                invitation.Trip.Status == Domain.Enums.TripStatus.PriceConfirmed &&
                invitation.Trip.MatchingAttempt == invitation.Attempt)
            .OrderBy(invitation => invitation.ExpiresAtUtc)
            .Take(limit)
            .ToListAsync(ct);
}
