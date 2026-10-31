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
            var expiry = DateTime.UtcNow.AddMinutes(-1);
            var results = await Task.WhenAll(
                repository.TryReserveAsync(drivers[0], trips[0], 1, expiry, ct),
                repository.TryReserveAsync(sameAttempt ? drivers[1] : drivers[0], sameAttempt ? trips[0] : trips[1], 1, expiry, ct));
            Assert.Single(results, result => result);
            var owner = await collection.Find(driver => driver.ReservationTripId != null &&
                (driver.DriverId == drivers[0] || driver.DriverId == drivers[1])).SingleAsync(ct);
            Assert.NotNull(await new MongoDriverRepository(database).FindReservationAsync(owner.ReservationTripId!.Value, 1, ct));
            Assert.True(await repository.TryReserveAsync(owner.DriverId, owner.ReservationTripId.Value, 1, expiry, ct));
            await repository.UpsertLocationAsync(owner.DriverId, owner.Location, true, ct);
            var stored = await collection.Find(driver => driver.DriverId == owner.DriverId).SingleAsync(ct);
            Assert.False(stored.IsAvailable);
            Assert.False(await repository.ReleaseReservationAsync(owner.DriverId, Guid.NewGuid(), 1, ct));
            Assert.True(await repository.ReleaseReservationAsync(owner.DriverId, owner.ReservationTripId.Value, 1, ct));
            Assert.False(await repository.ReleaseReservationAsync(owner.DriverId, owner.ReservationTripId.Value, 1, ct));
        }
        finally
        {
            await collection.DeleteManyAsync(Builders<DriverLocation>.Filter.In(driver => driver.DriverId, drivers), ct);
        }
    }
}
