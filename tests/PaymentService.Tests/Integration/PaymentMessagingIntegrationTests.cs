
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
        PaymentDbContext db,
        CreatePaymentRequest request,
        IPaymentDecisionService? decision = null)
    {
        var riderId = Guid.ParseExact(request.PayerAccount["rider-".Length..], "D");
        var driverId = Guid.ParseExact(request.PayeeAccount["driver-".Length..], "D");
        var trip = new TripSnapshot(
            request.TripId,
            riderId,
            driverId,
            request.Amount,
            TripStatusCode.PaymentPending);

        var tripClient = new Mock<ITripServiceClient>(MockBehavior.Strict);
        tripClient.Setup(x => x.GetByIdAsync(
                request.TripId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);

        return decision is null
            ? new PaymentApplicationService(new PaymentRepository(db), tripClient.Object)
            : new PaymentApplicationService(new PaymentRepository(db), tripClient.Object, decision);
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
            var result = await CreateService(db, request)
                .ProcessPaymentCommandAsync(request, ct);

            Assert.Equal("Succeeded", result.Status);
            Assert.False(result.IsTripSynced);
            firstId = result.Id;
        }

        await using (var db = new PaymentDbContext(options))
        {
            var result = await CreateService(db, request)
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
            await CreateService(db, request)
                .ProcessPaymentCommandAsync(request, ct);
        }

        var conflictingRequest = request with
        {
            IdempotencyKey = $"conflict-{Guid.NewGuid():N}"
        };

        await using (var db = new PaymentDbContext(options))
        {
            await Assert.ThrowsAsync<PaymentCommandRejectedException>(
                () => CreateService(db, request).ProcessPaymentCommandAsync(
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

            var result = await CreateService(db, request)
                .ProcessPaymentCommandAsync(request, ct);

            paymentId = result.Id;

            await transaction.RollbackAsync(ct);
        }

        await using var verify = new PaymentDbContext(options);

        Assert.Equal(0, await verify.Payments.CountAsync(
            x => x.TripId == request.TripId, ct));

        Assert.Equal(0, await verify.LedgerEntries.CountAsync(
            x => x.PaymentId == paymentId, ct));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DeclinedPayment_ShouldPersistWithoutLedgerAndReuseOriginalFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = await CreateOptionsAsync(ct);
        var request = NewRequest();
        var decision = new Mock<IPaymentDecisionService>(MockBehavior.Strict);
        decision.Setup(x => x.DecideAsync(
                It.IsAny<TripSnapshot>(),
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentDecision(
                false, "PAYMENT_DECLINED", "Thanh toán bị từ chối."));

        Guid paymentId;
        await using (var db = new PaymentDbContext(options))
        {
            var response = await CreateService(db, request, decision.Object)
                .ProcessPaymentCommandAsync(request, ct);
            Assert.Equal("Failed", response.Status);
            Assert.Equal("PAYMENT_DECLINED", response.FailureCode);
            paymentId = response.Id;
        }

        await using (var db = new PaymentDbContext(options))
        {
            var retry = await CreateService(db, request, decision.Object)
                .ProcessPaymentCommandAsync(request, ct);
            Assert.Equal(paymentId, retry.Id);
            Assert.Equal("Failed", retry.Status);
            Assert.Equal("Thanh toán bị từ chối.", retry.FailureReason);
        }

        await using var verify = new PaymentDbContext(options);
        var payment = await verify.Payments.AsNoTracking().SingleAsync(
            x => x.IdempotencyKey == request.IdempotencyKey, ct);
        Assert.Equal(paymentId, payment.Id);
        Assert.Equal("PAYMENT_DECLINED", payment.FailureCode);
        Assert.Equal(0, await verify.LedgerEntries.CountAsync(
            x => x.PaymentId == paymentId, ct));
        decision.Verify(x => x.DecideAsync(
            It.IsAny<TripSnapshot>(),
            It.IsAny<CreatePaymentRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DeclinedPayment_SameKeyDifferentPayload_ShouldNotCreateSecondPayment()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = await CreateOptionsAsync(ct);
        var request = NewRequest();
        var decision = new Mock<IPaymentDecisionService>();
        decision.Setup(x => x.DecideAsync(
                It.IsAny<TripSnapshot>(),
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentDecision(
                false, "PAYMENT_DECLINED", "Thanh toán bị từ chối."));

        await using (var db = new PaymentDbContext(options))
        {
            await CreateService(db, request, decision.Object)
                .ProcessPaymentCommandAsync(request, ct);
        }

        var changed = request with { Amount = request.Amount + 1m };
        await using (var db = new PaymentDbContext(options))
        {
            await Assert.ThrowsAsync<PaymentCommandRejectedException>(() =>
                CreateService(db, request, decision.Object)
                    .ProcessPaymentCommandAsync(changed, ct));
        }

        await using var verify = new PaymentDbContext(options);
        Assert.Equal(1, await verify.Payments.CountAsync(
            x => x.TripId == request.TripId, ct));
        var persistedId = await verify.Payments
            .Where(p => p.TripId == request.TripId)
            .Select(p => p.Id)
            .SingleAsync(ct);
        Assert.Equal(0, await verify.LedgerEntries.CountAsync(
            x => x.PaymentId == persistedId, ct));
    }
}
