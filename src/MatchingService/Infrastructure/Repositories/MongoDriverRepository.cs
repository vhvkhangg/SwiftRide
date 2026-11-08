using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Domain.Models;

namespace SwiftRide.MatchingService.Infrastructure.Repositories;

public sealed class MongoDriverRepository(IMongoDatabase database) : IDriverRepository
{
    private static readonly TimeSpan DriverLocationValidity = TimeSpan.FromMinutes(10);
    private static readonly BsonRegularExpression ValidDriverIdRegex =
        new("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");
    private static readonly FilterDefinition<DriverLocation> ValidDriverIdFilter =
        new BsonDocumentFilterDefinition<DriverLocation>(
            new BsonDocument("DriverId", ValidDriverIdRegex)) &
        Builders<DriverLocation>.Filter.Ne(driver => driver.DriverId, Guid.Empty.ToString("D"));

    private readonly IMongoCollection<DriverLocation> collection =
        database.GetCollection<DriverLocation>("drivers");

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var index = new CreateIndexModel<DriverLocation>(
            Builders<DriverLocation>.IndexKeys.Geo2DSphere(driver => driver.Location));
        await collection.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<DriverLocation>(
            Builders<DriverLocation>.IndexKeys.Ascending(driver => driver.DriverId),
            new CreateIndexOptions { Unique = true }), cancellationToken: cancellationToken);
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<DriverLocation>(
            Builders<DriverLocation>.IndexKeys.Ascending(driver => driver.ReservationTripId)
                .Ascending(driver => driver.ReservationAttempt),
            new CreateIndexOptions<DriverLocation>
            {
                Unique = true,
                PartialFilterExpression = new BsonDocument("ReservationTripId", new BsonDocument("$type", "binData"))
            }), cancellationToken: cancellationToken);
    }

    public async Task UpsertLocationAsync(
        string driverId,
        GeoJsonPoint<GeoJson2DGeographicCoordinates> location,
        bool isAvailable,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(driverId, "D", out var id) || id == Guid.Empty)
            throw new ArgumentException("Mã tài xế (DriverId) là bắt buộc.", nameof(driverId));
        if (!double.IsFinite(location.Coordinates.Latitude) || !double.IsFinite(location.Coordinates.Longitude) ||
            location.Coordinates.Latitude is < -90 or > 90 || location.Coordinates.Longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(location), "Invalid GPS coordinates.");

        driverId = id.ToString("D");
        var filter = Builders<DriverLocation>.Filter.Eq(driver => driver.DriverId, driverId);
        var update = Builders<DriverLocation>.Update.Pipeline(new BsonDocument[]
        {
            new("$set", new BsonDocument
            {
                { "DriverId", driverId },
                { "Location", location.ToBsonDocument() },
                { "UpdatedAt", DateTime.UtcNow },
                { "IsOnline", isAvailable },
                { "IsAvailable", new BsonDocument("$and", new BsonArray
                    {
                        isAvailable,
                        new BsonDocument("$eq", new BsonArray
                        {
                            new BsonDocument("$ifNull", new BsonArray { "$ReservationTripId", BsonNull.Value }),
                            BsonNull.Value
                        })
                    })
                }
            })
        });
        try
        {
            await collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        }
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
            Builders<DriverLocation>.Filter.Ne(driver => driver.IsOnline, false),
            Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationTripId, null),
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
            Builders<DriverLocation>.Filter.Ne(driver => driver.IsOnline, false),
            Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationTripId, null),
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
                Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationAttempt, attempt)))
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
            ValidDriverIdFilter,
            Builders<DriverLocation>.Filter.Ne(driver => driver.IsOnline, false),
            Builders<DriverLocation>.Filter.Gte(driver => driver.UpdatedAt, now - DriverLocationValidity),
            Builders<DriverLocation>.Filter.Eq(driver => driver.IsAvailable, true),
            Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationTripId, null));
        UpdateResult result;
        try
        {
            result = await collection.UpdateOneAsync(
                Builders<DriverLocation>.Filter.And(
                    Builders<DriverLocation>.Filter.Eq(driver => driver.DriverId, driverId),
                    available),
                Builders<DriverLocation>.Update
                    .Set(driver => driver.IsAvailable, false)
                    .Set(driver => driver.ReservationTripId, tripId)
                    .Set(driver => driver.ReservationAttempt, attempt)
                    .Set(driver => driver.ReservationExpiresAtUtc, expiresAtUtc),
                cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
        return result.MatchedCount > 0 || await collection.Find(owner &
            Builders<DriverLocation>.Filter.Eq(driver => driver.DriverId, driverId)).AnyAsync(cancellationToken);
    }

    public async Task<bool> ReleaseReservationAsync(
        string driverId,
        Guid tripId,
        int? attempt,
        CancellationToken cancellationToken)
    {
        var filter = Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationTripId, tripId);
        if (!string.IsNullOrEmpty(driverId))
            filter &= Builders<DriverLocation>.Filter.Eq(driver => driver.DriverId, driverId);
        if (attempt.HasValue)
            filter &= Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationAttempt, attempt.Value);
        var result = await collection.UpdateManyAsync(filter,
            Builders<DriverLocation>.Update.Pipeline(new BsonDocument[]
            {
                new("$set", new BsonDocument
                {
                    { "IsAvailable", new BsonDocument("$ifNull", new BsonArray { "$IsOnline", true }) },
                    { "ReservationTripId", "$$REMOVE" },
                    { "ReservationAttempt", "$$REMOVE" },
                    { "ReservationExpiresAtUtc", "$$REMOVE" }
                })
            }),
            cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<IReadOnlyList<DriverLocation>> GetReservationsToReconcileAsync(CancellationToken cancellationToken) =>
        await collection.Find(Builders<DriverLocation>.Filter.Ne(driver => driver.ReservationTripId, null) &
                Builders<DriverLocation>.Filter.Lte(driver => driver.ReservationExpiresAtUtc, DateTime.UtcNow))
            .SortBy(driver => driver.ReservationExpiresAtUtc).Limit(100).ToListAsync(cancellationToken);

    public Task DeferReconciliationAsync(string driverId, Guid tripId, CancellationToken cancellationToken) =>
        collection.UpdateOneAsync(
            Builders<DriverLocation>.Filter.Eq(driver => driver.DriverId, driverId) &
            Builders<DriverLocation>.Filter.Eq(driver => driver.ReservationTripId, tripId) &
            Builders<DriverLocation>.Filter.Ne(driver => driver.ReservationExpiresAtUtc, null),
            Builders<DriverLocation>.Update.Set(driver => driver.ReservationExpiresAtUtc, DateTime.UtcNow.AddMinutes(2)),
            cancellationToken: cancellationToken);
    public async Task<bool> ConfirmReservationAsync(
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
            Builders<DriverLocation>.Update.Unset(driver => driver.ReservationExpiresAtUtc),
            cancellationToken: cancellationToken);
        return result.ModifiedCount > 0 || result.MatchedCount > 0;
    }

    public async Task SetAvailabilityAsync(
        string driverId,
        bool isAvailable,
        CancellationToken cancellationToken)
    {
        var filter = Builders<DriverLocation>.Filter.Eq(driver => driver.DriverId, driverId);
        var result = await collection.UpdateOneAsync(
            filter,
            Builders<DriverLocation>.Update.Pipeline(new BsonDocument[]
            {
                new("$set", new BsonDocument
                {
                    { "IsOnline", isAvailable },
                    { "IsAvailable", new BsonDocument("$and", new BsonArray
                        {
                            isAvailable,
                            new BsonDocument("$eq", new BsonArray
                            {
                                new BsonDocument("$ifNull", new BsonArray { "$ReservationTripId", BsonNull.Value }),
                                BsonNull.Value
                            })
                        })
                    }
                })
            }),
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
