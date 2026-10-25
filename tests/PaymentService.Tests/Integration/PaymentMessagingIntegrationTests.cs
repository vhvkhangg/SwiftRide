
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using SwiftRide.PaymentService.Application.DTOs;
using SwiftRide.PaymentService.Application.Exceptions;
using SwiftRide.PaymentService.Application.Integrations;
using SwiftRide.PaymentService.Application.Services;
using SwiftRide.PaymentService.Infrastructure.Persistence;
using SwiftRide.PaymentService.Infrastructure.Repositories;
using Xunit;

namespace SwiftRide.PaymentService.Tests.Integration;

[Collection("Payment database")]
public sealed class PaymentMessagingIntegrationTests
{
    private static async Task<DbContextOptions<PaymentDbContext>>
        CreateOptionsAsync(CancellationToken ct)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "SWIFTRIDE_PAYMENT_TEST_DB");

        Assert.SkipUnless(
            !string.IsNullOrWhiteSpace(connectionString),
            "Cần cấu hình SWIFTRIDE_PAYMENT_TEST_DB.");

        var database = new NpgsqlConnectionStringBuilder(
            connectionString!).Database;

        if (database is null ||
            !database.EndsWith(
                "_test",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Integration tests chỉ chạy trên database _test.");
        }

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(connectionString!)
            .Options;

        await using var db = new PaymentDbContext(options);
        await db.Database.MigrateAsync(ct);

        return options;
    }

    private static CreatePaymentRequest NewRequest()
    {
        var riderId = Guid.NewGuid();
        var driverId = Guid.NewGuid();

        return new CreatePaymentRequest(
            Guid.NewGuid(),
            100_000m,
            $"trip-payment-{Guid.NewGuid():N}",
            $"rider-{riderId:D}",
            $"driver-{driverId:D}");
    }

    private static PaymentApplicationService CreateService(
        PaymentDbContext db)
    {
        // Messaging path không gọi REST TripService.
        var tripClient = Mock.Of<ITripServiceClient>();

        return new PaymentApplicationService(
            new PaymentRepository(db),
            tripClient);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SameBusinessKey_ShouldReuseOnePaymentAndTwoLedgers()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = await CreateOptionsAsync(ct);
        var request = NewRequest();

        Guid firstId;

        await using (var db = new PaymentDbContext(options))
        {
            var result = await CreateService(db)
                .ProcessPaymentCommandAsync(request, ct);

            Assert.Equal("Succeeded", result.Status);
            Assert.False(result.IsTripSynced);
            firstId = result.Id;
        }

        // Giả lập một lượt xử lý mới, với DbContext khác.
        await using (var db = new PaymentDbContext(options))
        {
            var result = await CreateService(db)
                .ProcessPaymentCommandAsync(request, ct);

            Assert.Equal(firstId, result.Id);
        }

        await using var verify = new PaymentDbContext(options);

        Assert.Equal(1, await verify.Payments.CountAsync(
            x => x.TripId == request.TripId, ct));

        Assert.Equal(2, await verify.LedgerEntries.CountAsync(
            x => x.PaymentId == firstId, ct));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SameTripDifferentKey_ShouldNotCreateAnotherPayment()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = await CreateOptionsAsync(ct);
        var request = NewRequest();

        await using (var db = new PaymentDbContext(options))
        {
            await CreateService(db)
                .ProcessPaymentCommandAsync(request, ct);
        }

        var conflictingRequest = request with
        {
            IdempotencyKey = $"conflict-{Guid.NewGuid():N}"
        };

        await using (var db = new PaymentDbContext(options))
        {
            await Assert.ThrowsAsync<PaymentCommandRejectedException>(
                () => CreateService(db).ProcessPaymentCommandAsync(
                    conflictingRequest, ct));
        }

        await using var verify = new PaymentDbContext(options);

        Assert.Equal(1, await verify.Payments.CountAsync(
            x => x.TripId == request.TripId, ct));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Rollback_ShouldNotPersistPaymentOrLedger()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = await CreateOptionsAsync(ct);
        var request = NewRequest();

        Guid paymentId;

        await using (var db = new PaymentDbContext(options))
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(ct);

            var result = await CreateService(db)
                .ProcessPaymentCommandAsync(request, ct);

            paymentId = result.Id;

            // Simulate consumer transaction rollback.
            await transaction.RollbackAsync(ct);
        }

        await using var verify = new PaymentDbContext(options);

        Assert.Equal(0, await verify.Payments.CountAsync(
            x => x.TripId == request.TripId, ct));

        Assert.Equal(0, await verify.LedgerEntries.CountAsync(
            x => x.PaymentId == paymentId, ct));
    }
}
