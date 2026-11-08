using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Microsoft.Extensions.Options;
using Moq;
using SwiftRide.Messaging;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.TripService.Application.Interfaces;
using SwiftRide.TripService.Application.Services;
using SwiftRide.TripService.Infrastructure.Repositories;
using SwiftRide.TripService.Domain.Entities;
using SwiftRide.TripService.Domain.Enums;
using SwiftRide.TripService.Infrastructure;
using Xunit;

namespace SwiftRide.TripService.Tests.Infrastructure;

public sealed class TripOutboxModelTests
{
    private static TripDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TripDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=model_test;Username=unused;Password=unused")
            .Options;

        return new TripDbContext(options);
    }

    [Fact]
    public void DbContext_MapsMassTransitInboxAndOutboxTables()
    {
        using var db = CreateContext();
        var tables = db.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .ToHashSet();

        Assert.Contains("InboxState", tables);
        Assert.Contains("OutboxMessage", tables);
        Assert.Contains("OutboxState", tables);
    }

    [Fact]
    public void OutboxMigration_IsRegistered()
    {
        using var db = CreateContext();
        var migrations = db.Database.GetMigrations();

        Assert.Contains(migrations, migration => migration.EndsWith("_AddTripOutbox", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------
    // Integration Test: Outbox Consistency & Atomic Transaction Verification
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UnitOfWork_PersistsTripStateAndOutboxMessageConsistentlyInSameTransaction()
    {
        await using var database = await TestDatabase.OpenAsync();
        var options = database.Options;

        await using var db = new TripDbContext(options);
        var unitOfWork = new TripUnitOfWork(db);
        var trip = Trip.Create(Guid.NewGuid(), "Điểm A", "Điểm B", 120_000m, 10.77, 106.69);
        var commandMessageId = Guid.NewGuid();

        await unitOfWork.ExecuteAsync(async ct =>
        {
            db.Trips.Add(trip);

            db.Set<OutboxMessage>().Add(new OutboxMessage
            {
                SequenceNumber = 1,
                MessageId = commandMessageId,
                ConversationId = trip.Id,
                CorrelationId = trip.Id,
                MessageType = "urn:message:SwiftRide.Contracts.Matching:FindDriverForTripV1",
                Body = $"{{\"tripId\":\"{trip.Id}\",\"attempt\":1}}",
                ContentType = "application/json",
                EnqueueTime = DateTime.UtcNow
            });

            await Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        await using var verifyDb = new TripDbContext(options);
        
        var savedTrip = await verifyDb.Trips.FirstOrDefaultAsync(
            t => t.Id == trip.Id, 
            TestContext.Current.CancellationToken);

        var savedOutboxMessage = await verifyDb.Set<OutboxMessage>().FirstOrDefaultAsync(
            m => m.MessageId == commandMessageId, 
            TestContext.Current.CancellationToken);

        Assert.NotNull(savedTrip);
        Assert.Equal(TripStatus.Requested, savedTrip.Status);
        Assert.Equal(120_000m, savedTrip.FareEstimate);

        Assert.NotNull(savedOutboxMessage);
        Assert.Equal(trip.Id, savedOutboxMessage.CorrelationId);
        Assert.Contains("FindDriverForTripV1", savedOutboxMessage.MessageType);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UnitOfWork_RollsBackBothTripStateAndOutboxMessageWhenErrorOccurs()
    {
        await using var database = await TestDatabase.OpenAsync();
        var options = database.Options;

        await using var db = new TripDbContext(options);
        var unitOfWork = new TripUnitOfWork(db);
        var trip = Trip.Create(Guid.NewGuid(), "Điểm C", "Điểm D", 50_000m);
        var commandMessageId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await unitOfWork.ExecuteAsync(async ct =>
            {
                db.Trips.Add(trip);
                db.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    SequenceNumber = 1,
                    MessageId = commandMessageId,
                    CorrelationId = trip.Id,
                    MessageType = "urn:message:SwiftRide.Contracts.Payments:ProcessPaymentV1",
                    Body = "{}",
                    ContentType = "application/json",
                    EnqueueTime = DateTime.UtcNow
                });

                await db.SaveChangesAsync(ct);
                throw new InvalidOperationException("Injected failure after SaveChanges.");
            }, TestContext.Current.CancellationToken);
        });

        await using var verifyDb = new TripDbContext(options);
        var savedTrip = await verifyDb.Trips.FirstOrDefaultAsync(
            t => t.Id == trip.Id, 
            TestContext.Current.CancellationToken);

        var savedOutboxMessage = await verifyDb.Set<OutboxMessage>().FirstOrDefaultAsync(
            m => m.MessageId == commandMessageId, 
            TestContext.Current.CancellationToken);

        Assert.Null(savedTrip);
        Assert.Null(savedOutboxMessage);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpgradeFromInitialSchema_PreservesRows_AndCanRunTwice()
    {
        await using var database = await TestDatabase.OpenAsync(migrate: false);
        await using var db = new TripDbContext(database.Options);
        var migrator = db.GetService<IMigrator>();
        var ct = TestContext.Current.CancellationToken;
        await migrator.MigrateAsync(db.Database.GetMigrations().First(), ct);
        var tripId = Guid.NewGuid();
        var riderId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Trips" ("Id", "RiderId", "PickupAddress", "DropoffAddress", "Status", "CreatedAt")
            VALUES ({tripId}, {riderId}, 'old pickup', 'old dropoff', 'Requested', now())
            """, ct);
        await db.Database.MigrateAsync(ct);
        await db.Database.MigrateAsync(ct);
        var trip = await db.Trips.SingleAsync(item => item.Id == tripId, ct);
        Assert.Equal(riderId, trip.RiderId);
        Assert.Equal("old pickup", trip.PickupAddress);
        Assert.Null(trip.IdempotencyKey);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConcurrentCreate_SameKeyAndPayload_ProducesOneTrip()
    {
        await using var database = await TestDatabase.OpenAsync();
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateTripRequest(Guid.NewGuid(), "A", "B", 10.77, 106.7, 5, 10,
            IdempotencyKey: "task5-" + Guid.NewGuid().ToString("N"));
        async Task<TripResponse> Create()
        {
            await using var db = new TripDbContext(database.Options);
            var repository = new TripRepository(db);
            var pricing = new Mock<IMatchingPricingClient>();
            pricing.Setup(item => item.GetQuoteAsync(It.IsAny<MatchingQuoteRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MatchingQuoteResponse(0, 50, 10, 0, 0, 0, 6, 66));
            var service = new TripApplicationService(repository, repository, new TripWorkflowRepository(db),
                new TripUnitOfWork(db), Mock.Of<ITripNotifier>(), pricing.Object, Mock.Of<ITripMessageBus>(),
                Options.Create(new TripMatchingOptions()));
            return await service.CreateAsync(request, ct);
        }
        var results = await Task.WhenAll(Create(), Create());
        Assert.Equal(results[0].Id, results[1].Id);
        await using var verify = new TripDbContext(database.Options);
        Assert.Equal(1, await verify.Trips.CountAsync(item => item.IdempotencyKey == request.IdempotencyKey, ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Integration")]
    public async Task ConcurrentInvitations_KeepOneOwner(bool sameDriver)
    {
        await using var database = await TestDatabase.OpenAsync();
        var ct = TestContext.Current.CancellationToken;
        var trip = Trip.Create(Guid.NewGuid(), "A", "B", 50000, 10.77, 106.7);
        trip.ConfirmPrice();
        trip.BeginMatchingAttempt(3);
        await using (var seed = new TripDbContext(database.Options))
        {
            seed.Trips.Add(trip);
            await seed.SaveChangesAsync(ct);
        }
        var firstDriver = Guid.NewGuid();
        async Task<bool> Invite(Guid driver)
        {
            await using var db = new TripDbContext(database.Options);
            try
            {
                return await new TripWorkflowRepository(db).AddInvitationAsync(new DriverInvitation
                {
                    TripId = trip.Id, DriverId = driver, Attempt = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(2)
                }, ct);
            }
            catch (InvalidOperationException) when (!sameDriver) { return false; }
        }
        var results = await Task.WhenAll(Invite(firstDriver), Invite(sameDriver ? firstDriver : Guid.NewGuid()));
        Assert.Single(results, result => result);
        await using var verify = new TripDbContext(database.Options);
        Assert.Single(await verify.DriverInvitations.ToListAsync(ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Integration")]
    public async Task AcceptRace_WithAcceptOrCancel_LeavesConsistentState(bool cancel)
    {
        await using var database = await TestDatabase.OpenAsync();
        var ct = TestContext.Current.CancellationToken;
        var driver = Guid.NewGuid();
        var trip = Trip.Create(Guid.NewGuid(), "A", "B", 50000, 10.77, 106.7);
        trip.ConfirmPrice();
        trip.BeginMatchingAttempt(3);
        await using (var seed = new TripDbContext(database.Options))
        {
            seed.Trips.Add(trip);
            seed.DriverInvitations.Add(new DriverInvitation
            {
                TripId = trip.Id, DriverId = driver, Attempt = 1,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(2)
            });
            await seed.SaveChangesAsync(ct);
        }
        var loaded = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Mutate(bool cancelling)
        {
            await using var db = new TripDbContext(database.Options);
            var repository = new TripRepository(db);
            var reader = new Mock<SwiftRide.TripService.Domain.Interfaces.ITripReader>();
            reader.Setup(item => item.GetByIdAsync(trip.Id, It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    var snapshot = await repository.GetByIdAsync(trip.Id, ct);
                    if (Interlocked.Increment(ref loaded) == 2) ready.SetResult();
                    await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
                    return snapshot;
                });
            var service = new TripApplicationService(repository, reader.Object, new TripWorkflowRepository(db),
                new TripUnitOfWork(db), Mock.Of<ITripNotifier>(), Mock.Of<IMatchingPricingClient>(),
                Mock.Of<ITripMessageBus>(), Options.Create(new TripMatchingOptions()));
            try
            {
                if (cancelling) await service.CancelAsync(trip.Id, ct);
                else await service.DriverAcceptAsync(trip.Id, new DriverAcceptRequest(driver), ct);
                return true;
            }
            catch (DbUpdateConcurrencyException) { return false; }
            catch (InvalidOperationException) { return false; }
        }
        var results = await Task.WhenAll(Mutate(false), Mutate(cancel));
        Assert.Single(results, result => result);
        await using var verify = new TripDbContext(database.Options);
        var stored = await verify.Trips.SingleAsync(ct);
        var invitation = await verify.DriverInvitations.SingleAsync(ct);
        if (stored.Status == TripStatus.DriverAccepted)
        {
            Assert.Equal(driver, stored.DriverId);
            Assert.True(invitation.IsResolved);
        }
        else
        {
            Assert.True(cancel);
            Assert.Equal(TripStatus.Cancelled, stored.Status);
            Assert.Null(stored.DriverId);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AcceptExpiredInvitation_DoesNotChangeTrip()
    {
        await using var database = await TestDatabase.OpenAsync();
        var ct = TestContext.Current.CancellationToken;
        await using var db = new TripDbContext(database.Options);
        var trip = Trip.Create(Guid.NewGuid(), "A", "B", 50000, 10.77, 106.7);
        trip.ConfirmPrice();
        trip.BeginMatchingAttempt(3);
        var driver = Guid.NewGuid();
        db.Trips.Add(trip);
        db.DriverInvitations.Add(new DriverInvitation
        {
            TripId = trip.Id, DriverId = driver, Attempt = 1,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1)
        });
        await db.SaveChangesAsync(ct);
        var repository = new TripRepository(db);
        var service = new TripApplicationService(repository, repository, new TripWorkflowRepository(db),
            new TripUnitOfWork(db), Mock.Of<ITripNotifier>(), Mock.Of<IMatchingPricingClient>(),
            Mock.Of<ITripMessageBus>(), Options.Create(new TripMatchingOptions()));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DriverAcceptAsync(trip.Id, new DriverAcceptRequest(driver), ct));
        await using var verify = new TripDbContext(database.Options);
        Assert.Equal(TripStatus.PriceConfirmed, (await verify.Trips.SingleAsync(ct)).Status);
        Assert.False((await verify.DriverInvitations.SingleAsync(ct)).IsResolved);
    }

    private sealed class TestDatabase(string connectionString, string schema) : IAsyncDisposable
    {
        public DbContextOptions<TripDbContext> Options => new DbContextOptionsBuilder<TripDbContext>()
            .UseNpgsql(new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString).Options;

        public static async Task<TestDatabase> OpenAsync(bool migrate = true)
        {
            var connectionString = Environment.GetEnvironmentVariable("SWIFTRIDE_TRIP_TEST_DB");
            Assert.SkipUnless(connectionString is not null || Environment.GetEnvironmentVariable("CI") == "true", "Set SWIFTRIDE_TRIP_TEST_DB.");
            if (connectionString is null || new NpgsqlConnectionStringBuilder(connectionString).Database?.EndsWith("_test", StringComparison.Ordinal) != true)
                throw new InvalidOperationException("Trip integration tests require an isolated _test database.");
            var schema = "task5_" + Guid.NewGuid().ToString("N");
            var database = new TestDatabase(connectionString, schema);
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE SCHEMA {schema}", connection);
            await command.ExecuteNonQueryAsync();
            try
            {
                if (migrate)
                {
                    await using var db = new TripDbContext(database.Options);
                    await db.Database.MigrateAsync();
                }
                return database;
            }
            catch { await database.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
