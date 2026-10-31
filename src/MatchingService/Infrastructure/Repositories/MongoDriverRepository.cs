using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Domain.Models;

namespace SwiftRide.MatchingService.Infrastructure.Repositories;

public sealed class MongoDriverRepository(IMongoClient mongoClient) : IDriverRepository
{
    private static readonly TimeSpan DriverLocationValidity = TimeSpan.FromMinutes(10);
    private static readonly BsonRegularExpression ValidDriverIdRegex =
        new("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$");
    private static readonly FilterDefinition<DriverLocation> ValidDriverIdFilter =
        new BsonDocumentFilterDefinition<DriverLocation>(
            new BsonDocument("DriverId", ValidDriverIdRegex));

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

        var now = DateTime.UtcNow;
        var filter = Builders<DriverLocation>.Filter.Eq(driver => driver.DriverId, driverId);
        var locationUpdate = Builders<DriverLocation>.Update
            .Set(driver => driver.Location, location)
            .Set(driver => driver.UpdatedAt, now);
        await collection.UpdateOneAsync(filter, locationUpdate, new UpdateOptions { IsUpsert = true }, cancellationToken);

        if (!isAvailable)
        {
            await collection.UpdateOneAsync(
                filter,
                Builders<DriverLocation>.Update.Set(driver => driver.IsAvailable, false),
                cancellationToken: cancellationToken);
            return;
        }

        var canBecomeAvailable = Builders<DriverLocation>.Filter.Or(
            Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationTripId, null),
            Builders<DriverLocation>.Filter.Lt(driver => driver.ReservationExpiresAtUtc, now));
        await collection.UpdateOneAsync(
            Builders<DriverLocation>.Filter.And(filter, canBecomeAvailable),
            Builders<DriverLocation>.Update
                .Set(driver => driver.IsAvailable, true)
                .Unset(driver => driver.ReservationTripId)
                .Unset(driver => driver.ReservationAttempt)
                .Unset(driver => driver.ReservationExpiresAtUtc),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<(DriverLocation Driver, double DistanceKm)>> FindNearbyAvailableAsync(
        double latitude,
        double longitude,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken)
    {
        var point = GeoJson.Point(GeoJson.Geographic(longitude, latitude));
        var minimumUpdatedAt = DateTime.UtcNow - DriverLocationValidity;
        var filter = Builders<DriverLocation>.Filter.And(
            Builders<DriverLocation>.Filter.Eq(driver => driver.IsAvailable, true),
            Builders<DriverLocation>.Filter.Gte(driver => driver.UpdatedAt, minimumUpdatedAt),
            ValidDriverIdFilter,
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

    public async Task<(DriverLocation Driver, double DistanceKm)?> FindNearestAvailableAsync(
        double latitude,
        double longitude,
        double radiusKm,
        IReadOnlyCollection<string> excludedDriverIds,
        CancellationToken cancellationToken)
    {
        var point = GeoJson.Point(GeoJson.Geographic(longitude, latitude));
        var filters = new List<FilterDefinition<DriverLocation>>
        {
            Builders<DriverLocation>.Filter.Eq(driver => driver.IsAvailable, true),
            Builders<DriverLocation>.Filter.Gte(driver => driver.UpdatedAt, DateTime.UtcNow - DriverLocationValidity),
            ValidDriverIdFilter,
            Builders<DriverLocation>.Filter.NearSphere(driver => driver.Location, point, radiusKm * 1000)
        };

        if (excludedDriverIds.Count > 0)
            filters.Add(Builders<DriverLocation>.Filter.Nin(driver => driver.DriverId, excludedDriverIds));

        var driver = await collection.Find(Builders<DriverLocation>.Filter.And(filters))
            .FirstOrDefaultAsync(cancellationToken);
        if (driver is null) return null;

        var coordinates = driver.Location.Coordinates;
        return (driver, Haversine(latitude, longitude, coordinates.Latitude, coordinates.Longitude));
    }

    public async Task<DriverLocation?> FindReservationAsync(
        Guid tripId,
        int attempt,
        CancellationToken cancellationToken)
    {
        return await collection.Find(Builders<DriverLocation>.Filter.And(
                Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationTripId, tripId),
                Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationAttempt, attempt),
                Builders<DriverLocation>.Filter.Gt(
                    driver => driver.ReservationExpiresAtUtc,
                    DateTime.UtcNow)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryReserveAsync(
        string driverId,
        Guid tripId,
        int attempt,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var owner = Builders<DriverLocation>.Filter.And(
            Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationTripId, tripId),
            Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationAttempt, attempt));
        var available = Builders<DriverLocation>.Filter.And(
            Builders<DriverLocation>.Filter.Eq(driver => driver.IsAvailable, true),
            Builders<DriverLocation>.Filter.Or(
                Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationTripId, null),
                Builders<DriverLocation>.Filter.Lt(driver => driver.ReservationExpiresAtUtc, now)));
        var result = await collection.UpdateOneAsync(
            Builders<DriverLocation>.Filter.And(
                Builders<DriverLocation>.Filter.Eq(driver => driver.DriverId, driverId),
                Builders<DriverLocation>.Filter.Or(owner, available)),
            Builders<DriverLocation>.Update
                .Set(driver => driver.IsAvailable, false)
                .Set(driver => driver.ReservationTripId, tripId)
                .Set(driver => driver.ReservationAttempt, attempt)
                .Set(driver => driver.ReservationExpiresAtUtc, expiresAtUtc),
            cancellationToken: cancellationToken);
        return result.ModifiedCount > 0 || result.MatchedCount > 0;
    }

    public async Task<bool> ReleaseReservationAsync(
        string driverId,
        Guid tripId,
        int attempt,
        CancellationToken cancellationToken)
    {
        var result = await collection.UpdateOneAsync(
            Builders<DriverLocation>.Filter.And(
                Builders<DriverLocation>.Filter.Eq(driver => driver.DriverId, driverId),
                Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationTripId, tripId),
                Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationAttempt, attempt)),
            Builders<DriverLocation>.Update
                .Set(driver => driver.IsAvailable, true)
                .Unset(driver => driver.ReservationTripId)
                .Unset(driver => driver.ReservationAttempt)
                .Unset(driver => driver.ReservationExpiresAtUtc),
            cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task SetAvailabilityAsync(
        string driverId,
        bool isAvailable,
        CancellationToken cancellationToken)
    {
        var filter = Builders<DriverLocation>.Filter.Eq(driver => driver.DriverId, driverId);
        FilterDefinition<DriverLocation> availabilityFilter = filter;
        if (isAvailable)
        {
            var now = DateTime.UtcNow;
            availabilityFilter = Builders<DriverLocation>.Filter.And(
                filter,
                Builders<DriverLocation>.Filter.Or(
                    Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationTripId, null),
                    Builders<DriverLocation>.Filter.Lt(driver => driver.ReservationExpiresAtUtc, now)));
        }

        var result = await collection.UpdateOneAsync(
            availabilityFilter,
            Builders<DriverLocation>.Update
                .Set(driver => driver.IsAvailable, isAvailable)
                .Set(driver => driver.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);
        if (result.MatchedCount == 0)
            throw new KeyNotFoundException($"Không tìm thấy vị trí tài xế '{driverId}'.");
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
