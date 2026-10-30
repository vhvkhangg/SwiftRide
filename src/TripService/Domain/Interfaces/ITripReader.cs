using System;
using System.Threading;
using System.Threading.Tasks;
using SwiftRide.TripService.Domain.Entities;

namespace SwiftRide.TripService.Domain.Interfaces;

public interface ITripReader
{
    Task<Trip?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Trip?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<Trip?> GetActiveTripAsync(Guid userId, string role, CancellationToken ct = default);
}
