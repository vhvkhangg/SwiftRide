using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using SwiftRide.MatchingService.Domain.Models;
using SwiftRide.MatchingService.Infrastructure.Repositories;

namespace SwiftRide.MatchingService.Tests.Infrastructure;

public sealed class MongoDriverRepositoryReservationTests
{
    [Fact]
    public async Task TryReserveAsync_AllowsOnlyOneOwnerAndIsIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = new MongoClient("mongodb://localhost:27017");
        var database = client.GetDatabase("swiftride_matching");
        var collection = database.GetCollection<DriverLocation>("drivers");
        var driverId = Guid.NewGuid().ToString("D");

        await collection.DeleteManyAsync(
            Builders<DriverLocation>.Filter.Eq(driver => driver.DriverId, driverId),
            cancellationToken);
        await collection.InsertOneAsync(new DriverLocation
        {
            DriverId = driverId,
            Location = GeoJson.Point(GeoJson.Geographic(106.7009, 10.7769)),
            IsAvailable = true,
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken: cancellationToken);

        try
        {
            var repository = new MongoDriverRepository(client);
            var expiry = DateTime.UtcNow.AddMinutes(2);
            var firstTrip = Guid.NewGuid();
            var secondTrip = Guid.NewGuid();

            var results = await Task.WhenAll(
                repository.TryReserveAsync(driverId, firstTrip, 1, expiry, cancellationToken),
                repository.TryReserveAsync(driverId, secondTrip, 1, expiry, cancellationToken));

            Assert.Equal(1, results.Count(result => result));
            Assert.True(await repository.TryReserveAsync(
                driverId, firstTrip, 1, expiry, cancellationToken)
                || await repository.TryReserveAsync(
                    driverId, secondTrip, 1, expiry, cancellationToken));

            var current = await collection.Find(
                    Builders<DriverLocation>.Filter.Eq(driver => driver.DriverId, driverId))
                    .SingleAsync(cancellationToken);
            Assert.False(current.IsAvailable);
            Assert.NotNull(current.ReservationTripId);
            Assert.Equal(1, current.ReservationAttempt);

            var wrongOwner = current.ReservationTripId == firstTrip ? secondTrip : firstTrip;
            Assert.False(await repository.ReleaseReservationAsync(
                driverId, wrongOwner, 1, cancellationToken));
            Assert.True(await repository.ReleaseReservationAsync(
                driverId, current.ReservationTripId!.Value, 1, cancellationToken));
        }
        finally
        {
            await collection.DeleteManyAsync(
                Builders<DriverLocation>.Filter.Eq(driver => driver.DriverId, driverId),
                cancellationToken);
        }
    }
}
