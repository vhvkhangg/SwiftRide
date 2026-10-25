using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
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
    public async Task UnitOfWork_PersistsTripStateAndOutboxMessageConsistentlyInSameTransaction()
    {
        var dbRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<TripDbContext>()
            .UseInMemoryDatabase($"outbox-persist-{Guid.NewGuid()}", dbRoot)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

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
    public async Task UnitOfWork_RollsBackBothTripStateAndOutboxMessageWhenErrorOccurs()
    {
        var dbRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<TripDbContext>()
            .UseInMemoryDatabase($"outbox-rollback-{Guid.NewGuid()}", dbRoot)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

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

                await Task.CompletedTask;
                throw new InvalidOperationException("Lỗi hệ thống bất ngờ!");
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
    }
}