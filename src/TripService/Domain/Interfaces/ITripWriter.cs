using System.Threading;
using System.Threading.Tasks;
using SwiftRide.TripService.Domain.Entities;

namespace SwiftRide.TripService.Domain.Interfaces;

public interface ITripWriter
{
    Task AddAsync(Trip trip, CancellationToken ct = default);
    Task SaveAsync(Trip trip, CancellationToken ct = default);
}
