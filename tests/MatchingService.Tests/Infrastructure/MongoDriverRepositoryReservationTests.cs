using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using SwiftRide.MatchingService.Domain.Models;
using SwiftRide.MatchingService.Infrastructure.Repositories;

namespace SwiftRide.MatchingService.Tests.Infrastructure;

[Trait("Category", "Integration")]
public sealed class MongoDriverRepositoryReservationTests
{
    private static IMongoDatabase Database()
    {
        var url = Environment.GetEnvironmentVariable("SWIFTRIDE_MATCHING_TEST_DB");
        Assert.SkipUnless(url is not null || Environment.GetEnvironmentVariable("CI") == "true",
            "Set SWIFTRIDE_MATCHING_TEST_DB to an isolated _test database.");
        if (url is null || MongoUrl.Create(url).DatabaseName?.EndsWith("_test", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Mongo tests require an isolated _test database.");
        return new MongoClient(url).GetDatabase(MongoUrl.Create(url).DatabaseName);
    }

    [Fact]
    public async Task OfflineAndStaleDrivers_CannotBeReservedOrMadeAvailableByRelease()
    {
        var database = Database();
        var repository = new MongoDriverRepository(database);
        var ct = TestContext.Current.CancellationToken;
        await repository.EnsureIndexesAsync(ct);
        var driverId = Guid.CreateVersion7().ToString("D");
        var tripId = Guid.NewGuid();
        var collection = database.GetCollection<DriverLocation>("drivers");
        var point = GeoJson.Point(GeoJson.Geographic(-122.4, 37.8));
        try
        {
            await repository.UpsertLocationAsync(driverId, point, true, ct);
            Assert.True(await repository.TryReserveAsync(driverId, tripId, 1, DateTime.UtcNow.AddMinutes(2), ct));
            await repository.UpsertLocationAsync(driverId, point, false, ct);
            Assert.True(await repository.ReleaseReservationAsync(driverId, tripId, 1, ct));
            var offline = await collection.Find(driver => driver.DriverId == driverId).SingleAsync(ct);
            Assert.False(offline.IsOnline);
            Assert.False(offline.IsAvailable);
            Assert.False(await repository.TryReserveAsync(driverId, Guid.NewGuid(), 1, DateTime.UtcNow.AddMinutes(2), ct));
            Assert.Empty(await repository.FindNearbyAvailableAsync(37.8, -122.4, 1, 10, ct));

            await repository.UpsertLocationAsync(driverId, point, true, ct);
            await collection.UpdateOneAsync(driver => driver.DriverId == driverId,
                Builders<DriverLocation>.Update.Set(driver => driver.UpdatedAt, DateTime.UtcNow.AddMinutes(-11)),
                cancellationToken: ct);
            Assert.False(await repository.TryReserveAsync(driverId, Guid.NewGuid(), 1, DateTime.UtcNow.AddMinutes(2), ct));
            Assert.Empty(await repository.FindNearbyAvailableAsync(37.8, -122.4, 1, 10, ct));
        }
        finally
        {
            await collection.DeleteOneAsync(driver => driver.DriverId == driverId, ct);
        }
    }

    [Fact]
    public async Task ConfirmedReservation_DuplicateReserveAndRecoveryCannotRestoreExpiry()
    {
        var database = Database();
        var repository = new MongoDriverRepository(database);
        var ct = TestContext.Current.CancellationToken;
        await repository.EnsureIndexesAsync(ct);
        var driverId = Guid.NewGuid().ToString("D");
        var tripId = Guid.NewGuid();
        var collection = database.GetCollection<DriverLocation>("drivers");
        try
        {
            await repository.UpsertLocationAsync(driverId, GeoJson.Point(GeoJson.Geographic(-122.4, 37.8)), true, ct);
            Assert.True(await repository.TryReserveAsync(driverId, tripId, 1, DateTime.UtcNow.AddMinutes(2), ct));
            Assert.False(await repository.TryReserveAsync(driverId, Guid.NewGuid(), 1, DateTime.UtcNow.AddMinutes(2), ct));
            Assert.True(await repository.ConfirmReservationAsync(driverId, tripId, 1, ct));
            Assert.True(await repository.TryReserveAsync(driverId, tripId, 1, DateTime.UtcNow.AddMinutes(2), ct));
            await repository.DeferReconciliationAsync(driverId, tripId, ct);
            var stored = await collection.Find(driver => driver.DriverId == driverId).SingleAsync(ct);
            Assert.Null(stored.ReservationExpiresAtUtc);
            Assert.False(stored.IsAvailable);
        }
        finally
        {
            await collection.DeleteOneAsync(driver => driver.DriverId == driverId, ct);
        }
    }

    [Fact]
    public async Task ConcurrentFirstHeartbeats_KeepOneDriverAndReservation()
    {
        var database = Database();
        var repository = new MongoDriverRepository(database);
        var ct = TestContext.Current.CancellationToken;
        await repository.EnsureIndexesAsync(ct);
        var driverId = Guid.NewGuid().ToString("D");
        var tripId = Guid.NewGuid();
        var collection = database.GetCollection<DriverLocation>("drivers");
        var point = GeoJson.Point(GeoJson.Geographic(-122.4, 37.8));
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => repository.UpsertLocationAsync(driverId, point, true, ct)));
            Assert.Equal(1, await collection.CountDocumentsAsync(driver => driver.DriverId == driverId, cancellationToken: ct));
            Assert.True(await repository.TryReserveAsync(driverId, tripId, 1, DateTime.UtcNow.AddMinutes(2), ct));
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => repository.UpsertLocationAsync(driverId, point, true, ct)));
            Assert.False((await collection.Find(driver => driver.DriverId == driverId).SingleAsync(ct)).IsAvailable);
            Assert.True(await repository.ReleaseReservationAsync(driverId, tripId, 1, ct));
            Assert.True(await repository.TryReserveAsync(driverId, tripId, 2, DateTime.UtcNow.AddMinutes(2), ct));
            Assert.False(await repository.ReleaseReservationAsync(driverId, tripId, 1, ct));
            Assert.NotNull(await repository.FindReservationAsync(tripId, 2, ct));
        }
        finally
        {
            await collection.DeleteOneAsync(driver => driver.DriverId == driverId, ct);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reservations_HaveOneDriverAndOneOwner(bool sameAttempt)
    {
        var database = Database();
        var repository = new MongoDriverRepository(database);
        var ct = TestContext.Current.CancellationToken;
        await repository.EnsureIndexesAsync(ct);
        var drivers = new[] { Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D") };
        var trips = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var collection = database.GetCollection<DriverLocation>("drivers");
        try
        {
            foreach (var driver in drivers)
                await repository.UpsertLocationAsync(driver, GeoJson.Point(GeoJson.Geographic(106.7, 10.77)), true, ct);
            var expiry = DateTime.UtcNow.AddMinutes(2);
            var results = await Task.WhenAll(
                repository.TryReserveAsync(drivers[0], trips[0], 1, expiry, ct),
                repository.TryReserveAsync(sameAttempt ? drivers[1] : drivers[0], sameAttempt ? trips[0] : trips[1], 1, expiry, ct));
            Assert.Single(results, result => result);
            var owner = await collection.Find(driver => driver.ReservationTripId != null &&
                (driver.DriverId == drivers[0] || driver.DriverId == drivers[1])).SingleAsync(ct);
            var restored = new MongoDriverRepository(database);
            Assert.Equal(owner.DriverId, (await restored.FindReservationAsync(owner.ReservationTripId!.Value, 1, ct))?.DriverId);
            Assert.Null(await restored.FindReservationAsync(owner.ReservationTripId.Value, 2, ct));
            Assert.True(await repository.TryReserveAsync(owner.DriverId, owner.ReservationTripId.Value, 1, expiry, ct));
            await repository.UpsertLocationAsync(owner.DriverId, owner.Location, true, ct);
            var stored = await collection.Find(driver => driver.DriverId == owner.DriverId).SingleAsync(ct);
            Assert.False(stored.IsAvailable);
            Assert.False(await repository.ReleaseReservationAsync(owner.DriverId, Guid.NewGuid(), 1, ct));
            Assert.True(await repository.ReleaseReservationAsync(owner.DriverId, owner.ReservationTripId.Value, 1, ct));
            Assert.Null(await restored.FindReservationAsync(owner.ReservationTripId.Value, 1, ct));
            Assert.False(await repository.ReleaseReservationAsync(owner.DriverId, owner.ReservationTripId.Value, 1, ct));
        }
        finally
        {
            await collection.DeleteManyAsync(Builders<DriverLocation>.Filter.In(driver => driver.DriverId, drivers), ct);
        }
    }
}
