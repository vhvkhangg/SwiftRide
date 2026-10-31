using MongoDB.Driver.GeoJsonObjectModel;
using SwiftRide.MatchingService.Domain.Models;

namespace SwiftRide.MatchingService.Domain.Interfaces;

public interface IDriverRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken);
    Task UpsertLocationAsync(string driverId, GeoJsonPoint<GeoJson2DGeographicCoordinates> location, bool isAvailable, CancellationToken cancellationToken);
    Task<IReadOnlyList<(DriverLocation Driver, double DistanceKm)>> FindNearbyAvailableAsync(double latitude, double longitude, double radiusKm, int limit, CancellationToken cancellationToken);
    Task<(DriverLocation Driver, double DistanceKm)?> FindNearestAvailableAsync(
        double latitude,
        double longitude,
        double radiusKm,
        IReadOnlyCollection<string> excludedDriverIds,
        CancellationToken cancellationToken);
    Task<DriverLocation?> FindReservationAsync(
        Guid tripId,
        int attempt,
        CancellationToken cancellationToken);
    Task<bool> TryReserveAsync(
        string driverId,
        Guid tripId,
        int attempt,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken);
    Task<bool> ReleaseReservationAsync(
        string driverId,
        Guid tripId,
        int? attempt,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<DriverLocation>> GetReservationsToReconcileAsync(CancellationToken cancellationToken);
    Task DeferReconciliationAsync(string driverId, Guid tripId, CancellationToken cancellationToken);
    Task SetAvailabilityAsync(string driverId, bool isAvailable, CancellationToken cancellationToken);
}
