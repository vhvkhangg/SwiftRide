using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using SwiftRide.MatchingService.Domain.Models;
using SwiftRide.MatchingService.Infrastructure.Repositories;

namespace SwiftRide.MatchingService.Tests.Infrastructure;

[Trait("Category", "Integration")]
public sealed class MongoMatchingOutboxTests
{
    private static IMongoDatabase Database()
    {
        var url = Environment.GetEnvironmentVariable("SWIFTRIDE_MATCHING_TEST_DB");
        Assert.SkipUnless(url is not null, "Set SWIFTRIDE_MATCHING_TEST_DB to an isolated _test database.");
        if (MongoUrl.Create(url!).DatabaseName?.EndsWith("_test", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Mongo tests require an isolated _test database.");
        var client = new MongoClient(url);
        var databaseName = $"{MongoUrl.Create(url).DatabaseName}_{Guid.NewGuid():N}_test";
        return client.GetDatabase(databaseName);
    }

    [Fact]
    public async Task ReservationAndDriverFoundEvent_AreCommittedTogether()
    {
        var database = Database();
        var drivers = new MongoDriverRepository(database);
        var outbox = new MongoMatchingOutboxRepository(database);
        var ct = TestContext.Current.CancellationToken;
        await drivers.EnsureIndexesAsync(ct);
        await outbox.EnsureIndexesAsync(ct);
        var driverId = Guid.NewGuid().ToString("D");
        var tripId = Guid.NewGuid();
        var driverCollection = database.GetCollection<DriverLocation>("drivers");
        var outboxCollection = database.GetCollection<MatchingOutboxMessage>("matching_outbox");

        try
        {
            Assert.False(await outbox.ExistsAsync(tripId, 1, ct));
            await drivers.UpsertLocationAsync(
                driverId,
                GeoJson.Point(GeoJson.Geographic(106.7, 10.77)),
                true,
                ct);

            Assert.True(await drivers.TryReserveAsync(
                driverId, tripId, 1, DateTime.UtcNow.AddMinutes(2), ct));

            var reservation = await driverCollection.Find(driver => driver.DriverId == driverId).SingleAsync(ct);
            var message = await outboxCollection.Find(item =>
                item.Id == MatchingOutboxMessage.CreateId(tripId, 1)).SingleAsync(ct);

            Assert.Equal(tripId, reservation.ReservationTripId);
            Assert.Equal(driverId, message.DriverId?.ToString("D"));
            Assert.Equal(MatchingOutboxMessage.DriverFoundType, message.Type);
            Assert.Equal(MatchingOutboxMessage.PendingStatus, message.Status);
            Assert.True(await outbox.ExistsAsync(tripId, 1, ct));
            Assert.False(await outbox.EnqueueNoDriverFoundAsync(tripId, 1, "late duplicate", ct));
        }
        finally
        {
            await database.Client.DropDatabaseAsync(database.DatabaseNamespace.DatabaseName, ct);
        }
    }

    [Fact]
    public async Task NoDriverOutcome_PreventsConflictingReservationForSameAttempt()
    {
        var database = Database();
        var drivers = new MongoDriverRepository(database);
        var outbox = new MongoMatchingOutboxRepository(database);
        var ct = TestContext.Current.CancellationToken;
        await drivers.EnsureIndexesAsync(ct);
        await outbox.EnsureIndexesAsync(ct);
        var driverId = Guid.NewGuid().ToString("D");
        var tripId = Guid.NewGuid();
        var driverCollection = database.GetCollection<DriverLocation>("drivers");
        var outboxCollection = database.GetCollection<MatchingOutboxMessage>("matching_outbox");

        try
        {
            Assert.True(await outbox.EnqueueNoDriverFoundAsync(tripId, 1, "none available", ct));
            Assert.True(await outbox.ExistsAsync(tripId, 1, ct));
            await drivers.UpsertLocationAsync(
                driverId,
                GeoJson.Point(GeoJson.Geographic(106.7, 10.77)),
                true,
                ct);

            Assert.False(await drivers.TryReserveAsync(
                driverId, tripId, 1, DateTime.UtcNow.AddMinutes(2), ct));
            var driver = await driverCollection.Find(item => item.DriverId == driverId).SingleAsync(ct);
            var result = await outboxCollection.Find(item =>
                item.Id == MatchingOutboxMessage.CreateId(tripId, 1)).SingleAsync(ct);

            Assert.True(driver.IsAvailable);
            Assert.Equal(MatchingOutboxMessage.NoDriverFoundType, result.Type);
        }
        finally
        {
            await database.Client.DropDatabaseAsync(database.DatabaseNamespace.DatabaseName, ct);
        }
    }

    [Fact]
    public async Task ExpiredDispatchLease_CanBeRecoveredAndMarkedDelivered()
    {
        var database = Database();
        var outbox = new MongoMatchingOutboxRepository(database);
        var ct = TestContext.Current.CancellationToken;
        await outbox.EnsureIndexesAsync(ct);
        var tripId = Guid.NewGuid();
        var messageId = MatchingOutboxMessage.CreateId(tripId, 1);
        var firstLock = Guid.NewGuid();
        var secondLock = Guid.NewGuid();
        var thirdLock = Guid.NewGuid();

        try
        {
            Assert.True(await outbox.EnqueueNoDriverFoundAsync(tripId, 1, "none available", ct));
            // Enqueue timestamps with its own wall clock; start the simulated lease timeline
            // after that timestamp so the first claim is due even on coarse-resolution clocks.
            var now = DateTime.UtcNow.AddMinutes(1);
            var claimed = await outbox.ClaimNextAsync(firstLock, now, TimeSpan.FromMinutes(1), ct);
            Assert.NotNull(claimed);
            Assert.Null(await outbox.ClaimNextAsync(
                secondLock, now.AddSeconds(30), TimeSpan.FromMinutes(1), ct));

            var recovered = await outbox.ClaimNextAsync(
                secondLock, now.AddMinutes(2), TimeSpan.FromMinutes(1), ct);
            Assert.NotNull(recovered);
            Assert.Equal(messageId, recovered.Id);
            await outbox.ScheduleRetryAsync(
                messageId, secondLock, now.AddMinutes(3), "broker unavailable", ct);
            Assert.Null(await outbox.ClaimNextAsync(
                thirdLock, now.AddMinutes(2.5), TimeSpan.FromMinutes(1), ct));
            var retried = await outbox.ClaimNextAsync(
                thirdLock, now.AddMinutes(4), TimeSpan.FromMinutes(1), ct);
            Assert.NotNull(retried);
            Assert.Equal(3, retried.PublishAttempts);
            await outbox.MarkDeliveredAsync(messageId, thirdLock, now.AddMinutes(4), ct);

            var delivered = await database.GetCollection<MatchingOutboxMessage>("matching_outbox")
                .Find(item => item.Id == messageId)
                .SingleAsync(ct);
            Assert.Equal(MatchingOutboxMessage.DeliveredStatus, delivered.Status);
            Assert.NotNull(delivered.DeliveredAtUtc);
        }
        finally
        {
            await database.Client.DropDatabaseAsync(database.DatabaseNamespace.DatabaseName, ct);
        }
    }
}
