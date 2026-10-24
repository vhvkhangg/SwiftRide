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
    Task SetAvailabilityAsync(string driverId, bool isAvailable, CancellationToken cancellationToken);
}
