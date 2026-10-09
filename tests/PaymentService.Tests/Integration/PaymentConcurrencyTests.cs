using Microsoft.EntityFrameworkCore;
using Npgsql;
using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Application.Services;
using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Infrastructure.Persistence;
using SwiftRide.PaymentService.Infrastructure.Repositories;
using Xunit;

namespace SwiftRide.PaymentService.Tests.Integration;

public sealed class PaymentConcurrencyTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConcurrentSameKey_ShouldProduceSinglePaymentAndTwoLedgerRows()
    {
        var connectionString = Environment.GetEnvironmentVariable("SWIFTRIDE_PAYMENT_TEST_DB");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(connectionString),
            "Chỉ chạy khi đã cấu hình SWIFTRIDE_PAYMENT_TEST_DB.");
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString!).Database;
        if (databaseName is null || !databaseName.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ cho phép integration test trên DB tên kết thúc _test.");

        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(connectionString!).Options;
        await using (var migrate = new PaymentDbContext(options))
            await migrate.Database.MigrateAsync(ct);

        var tripId = Guid.NewGuid();
        var riderId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var request = new CreatePaymentRequest(
            tripId, 100_000m, $"race-{Guid.NewGuid():N}",
            $"rider-{riderId:D}", $"driver-{driverId:D}");
        var fakeTrip = new BarrierTripClient(new TripSnapshot(
            tripId, riderId, driverId, 100_000m, TripStatusCode.PaymentPending));

        await using var dbA = new PaymentDbContext(options);
        await using var dbB = new PaymentDbContext(options);
        var serviceA = new PaymentApplicationService(new PaymentRepository(dbA), fakeTrip);
        var serviceB = new PaymentApplicationService(new PaymentRepository(dbB), fakeTrip);

        var responses = await Task.WhenAll(
            serviceA.CreatePaymentAsync(request, ct),
            serviceB.CreatePaymentAsync(request, ct));
        Assert.Equal(responses[0].Id, responses[1].Id);

        await using var verify = new PaymentDbContext(options);
        var actual = await verify.Payments.AsNoTracking()
            .Where(p => p.IdempotencyKey == request.IdempotencyKey).ToListAsync(ct);
        var payment = Assert.Single(actual);
        Assert.True(payment.IsTripSynced);
        Assert.Equal(2, await verify.LedgerEntries.CountAsync(x => x.PaymentId == payment.Id, ct));
    }

    private sealed class BarrierTripClient : ITripServiceClient
    {
        private readonly TripSnapshot _trip;
        private readonly TaskCompletionSource<bool> _bothReached =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requests;

        public BarrierTripClient(TripSnapshot trip) => _trip = trip;

        public async Task<TripSnapshot?> GetByIdAsync(Guid tripId,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _requests) == 2)
                _bothReached.TrySetResult(true);
            await _bothReached.Task.WaitAsync(cancellationToken);
            return _trip;
        }

        public Task MarkPaidAsync(Guid tripId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConcurrentExistingSameKey_SyncConflict_ShouldReturnSuccessToBoth()
    {
        var connectionString = Environment.GetEnvironmentVariable("SWIFTRIDE_PAYMENT_TEST_DB");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(connectionString),
            "Chỉ chạy khi đã cấu hình SWIFTRIDE_PAYMENT_TEST_DB.");
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString!).Database;
        if (databaseName is null || !databaseName.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ cho phép integration test trên DB tên kết thúc _test.");

        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(connectionString!).Options;
        await using (var migrate = new PaymentDbContext(options))
            await migrate.Database.MigrateAsync(ct);

        var tripId = Guid.NewGuid();
        var rider = Guid.NewGuid();
        var driver = Guid.NewGuid();
        var request = new CreatePaymentRequest(
            tripId, 100_000m, $"existing-race-{Guid.NewGuid():N}",
            $"rider-{rider:D}", $"driver-{driver:D}");
        var payment = Payment.Create(
            tripId, request.Amount, request.IdempotencyKey,
            request.PayerAccount, request.PayeeAccount);
        payment.MarkSucceeded();

        await using (var seed = new PaymentDbContext(options))
        {
            seed.Payments.Add(payment);
            await seed.SaveChangesAsync(ct);
        }

        // Both requests must read the same unsynced row before either callback can finish.
        var tripClient = new BarrierSyncTripClient();
        await using var dbA = new PaymentDbContext(options);
        await using var dbB = new PaymentDbContext(options);
        var serviceA = new PaymentApplicationService(new PaymentRepository(dbA), tripClient);
        var serviceB = new PaymentApplicationService(new PaymentRepository(dbB), tripClient);

        var responses = await Task.WhenAll(
            serviceA.CreatePaymentAsync(request, ct),
            serviceB.CreatePaymentAsync(request, ct));

        Assert.All(responses, response =>
        {
            Assert.Equal(payment.Id, response.Id);
            Assert.True(response.IsTripSynced);
        });
        Assert.Equal(2, tripClient.CallbackCount);

        await using var verify = new PaymentDbContext(options);
        var persisted = await verify.Payments.AsNoTracking()
            .SingleAsync(x => x.Id == payment.Id, ct);
        Assert.True(persisted.IsTripSynced);
        Assert.Equal(2L, persisted.Version);
        Assert.Equal(1, await verify.Payments.CountAsync(
            x => x.IdempotencyKey == request.IdempotencyKey, ct));
    }

    private sealed class BarrierSyncTripClient : ITripServiceClient
    {
        private readonly TaskCompletionSource<bool> _bothCallbacks =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callbacks;

        public int CallbackCount => Volatile.Read(ref _callbacks);

        public Task<TripSnapshot?> GetByIdAsync(
            Guid tripId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TripSnapshot?>(null);

        public async Task MarkPaidAsync(
            Guid tripId, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _callbacks) == 2)
                _bothCallbacks.TrySetResult(true);
            await _bothCallbacks.Task.WaitAsync(cancellationToken);
        }
    }
}
