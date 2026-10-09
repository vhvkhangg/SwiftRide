using MongoDB.Driver.GeoJsonObjectModel;
using SwiftRide.MatchingService.Domain.Models;

namespace SwiftRide.MatchingService.Domain.Interfaces;

public interface IDriverRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken);
    Task UpsertLocationAsync(string driverId, GeoJsonPoint<GeoJson2DGeographicCoordinates> location, bool isAvailable, CancellationToken cancellationToken);
    Task<IReadOnlyList<(DriverLocation Driver, double DistanceKm)>> FindNearbyAvailableAsync(double latitude, double longitude, double radiusKm, int limit, CancellationToken cancellationToken);
}
