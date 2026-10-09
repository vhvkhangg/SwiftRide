using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Domain.Models;

namespace SwiftRide.MatchingService.Infrastructure.Repositories;

public sealed class MongoDriverRepository(IMongoClient mongoClient) : IDriverRepository
{
    private readonly IMongoCollection<DriverLocation> collection =
        mongoClient.GetDatabase("swiftride_matching").GetCollection<DriverLocation>("drivers");

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var index = new CreateIndexModel<DriverLocation>(
            Builders<DriverLocation>.IndexKeys.Geo2DSphere(driver => driver.Location));
        await collection.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }

    public async Task UpsertLocationAsync(
        string driverId,
        GeoJsonPoint<GeoJson2DGeographicCoordinates> location,
        bool isAvailable,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(driverId))
            throw new ArgumentException("Mã tài xế (DriverId) là bắt buộc.", nameof(driverId));

        var filter = Builders<DriverLocation>.Filter.Eq(driver => driver.DriverId, driverId);
        var update = Builders<DriverLocation>.Update
            .Set(driver => driver.Location, location)
            .Set(driver => driver.IsAvailable, isAvailable)
            .Set(driver => driver.UpdatedAt, DateTime.UtcNow);
        await collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<IReadOnlyList<(DriverLocation Driver, double DistanceKm)>> FindNearbyAvailableAsync(
        double latitude,
        double longitude,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken)
    {
        var point = GeoJson.Point(GeoJson.Geographic(longitude, latitude));
        var filter = Builders<DriverLocation>.Filter.And(
            Builders<DriverLocation>.Filter.Eq(driver => driver.IsAvailable, true),
            Builders<DriverLocation>.Filter.NearSphere(driver => driver.Location, point, radiusKm * 1000));
        var drivers = await collection.Find(filter).Limit(limit).ToListAsync(cancellationToken);
        return drivers
            .Select(driver =>
            {
                var coordinates = driver.Location.Coordinates;
                return (driver, DistanceKm: Haversine(latitude, longitude, coordinates.Latitude, coordinates.Longitude));
            })
            .OrderBy(item => item.DistanceKm)
            .ToArray();
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371;
        var lat = (lat2 - lat1) * Math.PI / 180;
        var lon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(lat / 2) * Math.Sin(lat / 2)
            + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
            * Math.Sin(lon / 2) * Math.Sin(lon / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
