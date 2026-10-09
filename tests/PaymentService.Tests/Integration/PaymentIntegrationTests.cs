
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Domain.Enums;
using SwiftRide.PaymentService.Infrastructure.Persistence;
using Xunit;

namespace SwiftRide.PaymentService.Tests.Integration;

public sealed class PaymentWeek4IntegrationTests
{
    private static async Task<DbContextOptions<PaymentDbContext>>
        CreateOptionsAsync(CancellationToken ct)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "SWIFTRIDE_PAYMENT_TEST_DB");

        Assert.SkipUnless(
            !string.IsNullOrWhiteSpace(connectionString),
            "Cần cấu hình SWIFTRIDE_PAYMENT_TEST_DB.");

        var databaseName = new NpgsqlConnectionStringBuilder(
            connectionString!).Database;

        if (databaseName is null ||
            !databaseName.EndsWith(
                "_test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Integration Tests chỉ được chạy trên database _test.");
        }

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(connectionString!)
            .Options;

        await using var db = new PaymentDbContext(options);
        await db.Database.MigrateAsync(ct);

        return options;
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UniqueTripIndex_ShouldRejectSecondPayment(
        bool firstWasRefunded)
    {
        var ct = TestContext.Current.CancellationToken;
        var options = await CreateOptionsAsync(ct);
        var tripId = Guid.NewGuid();

        var first = Payment.Create(
            tripId,
            100_000m,
            $"first-{Guid.NewGuid():N}",
            "rider-001",
            "driver-001");

        first.MarkSucceeded();

        if (firstWasRefunded)
            first.Refund();

        await using (var db = new PaymentDbContext(options))
        {
            db.Payments.Add(first);
            await db.SaveChangesAsync(ct);
        }

        var second = Payment.Create(
            tripId,
            100_000m,
            $"second-{Guid.NewGuid():N}",
            "rider-001",
            "driver-001");

        second.MarkSucceeded();

        await using (var db = new PaymentDbContext(options))
        {
            db.Payments.Add(second);

            var exception =
                await Assert.ThrowsAsync<DbUpdateException>(
                    async () =>
                    {
                        await db.SaveChangesAsync(ct);
                    });

            var postgres = Assert.IsType<PostgresException>(
                exception.InnerException);

            Assert.Equal(
                PostgresErrorCodes.UniqueViolation,
                postgres.SqlState);

            Assert.Equal(
                "ux_payments_settled_trip",
                postgres.ConstraintName);
        }

        await using (var verify = new PaymentDbContext(options))
        {
            var payments = await verify.Payments
                .AsNoTracking()
                .Where(x => x.TripId == tripId)
                .ToListAsync(ct);

            var stored = Assert.Single(payments);

            Assert.Equal(first.Id, stored.Id);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task FailedPayment_ShouldAllowLaterSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = await CreateOptionsAsync(ct);
        var tripId = Guid.NewGuid();

        var failed = Payment.Create(
            tripId,
            100_000m,
            $"failed-{Guid.NewGuid():N}",
            "rider-001",
            "driver-001");

        failed.MarkFailed();

        var succeeded = Payment.Create(
            tripId,
            100_000m,
            $"succeeded-{Guid.NewGuid():N}",
            "rider-001",
            "driver-001");

        succeeded.MarkSucceeded();

        await using (var db = new PaymentDbContext(options))
        {
            db.Payments.AddRange(failed, succeeded);
            await db.SaveChangesAsync(ct);
        }

        await using (var verify = new PaymentDbContext(options))
        {
            var payments = await verify.Payments
                .AsNoTracking()
                .Where(x => x.TripId == tripId)
                .ToListAsync(ct);

            Assert.Equal(2, payments.Count);

            Assert.Contains(payments,
                x => x.Status == PaymentStatus.Failed);

            Assert.Contains(payments,
                x => x.Status == PaymentStatus.Succeeded);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConcurrentRefund_ShouldRollbackLosingRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = await CreateOptionsAsync(ct);

        var payment = Payment.Create(
            Guid.NewGuid(),
            100_000m,
            $"refund-race-{Guid.NewGuid():N}",
            "rider-001",
            "driver-001");

        payment.MarkSucceeded();

        var originalEntries = new[]
        {
            LedgerEntry.Create(
                payment.Id,
                LedgerEntryType.Debit,
                payment.PayerAccount,
                payment.Amount),

            LedgerEntry.Create(
                payment.Id,
                LedgerEntryType.Credit,
                payment.PayeeAccount,
                payment.Amount)
        };

        // Persist Payment and original ledger.
        await using (var setup = new PaymentDbContext(options))
        {
            setup.Payments.Add(payment);
            setup.LedgerEntries.AddRange(originalEntries);

            await setup.SaveChangesAsync(ct);
        }

        // Both contexts read the same initial Version.
        await using var dbA = new PaymentDbContext(options);
        await using var dbB = new PaymentDbContext(options);

        var paymentA = await dbA.Payments
            .SingleAsync(x => x.Id == payment.Id, ct);

        var paymentB = await dbB.Payments
            .SingleAsync(x => x.Id == payment.Id, ct);

        Assert.Equal(paymentA.Version, paymentB.Version);

        // Request A refunds successfully.
        paymentA.Refund();

        dbA.LedgerEntries.AddRange(
            LedgerEntry.Create(
                paymentA.Id,
                LedgerEntryType.Credit,
                paymentA.PayerAccount,
                paymentA.Amount),

            LedgerEntry.Create(
                paymentA.Id,
                LedgerEntryType.Debit,
                paymentA.PayeeAccount,
                paymentA.Amount));

        await dbA.SaveChangesAsync(ct);

        // Request B uses the stale Version.
        paymentB.Refund();

        dbB.LedgerEntries.AddRange(
            LedgerEntry.Create(
                paymentB.Id,
                LedgerEntryType.Credit,
                paymentB.PayerAccount,
                paymentB.Amount),

            LedgerEntry.Create(
                paymentB.Id,
                LedgerEntryType.Debit,
                paymentB.PayeeAccount,
                paymentB.Amount));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            async () =>
            {
                await dbB.SaveChangesAsync(ct);
            });

        // Verify: 2 original + 2 reversal entries.
        // The losing request must not commit its entries.
        await using var verify = new PaymentDbContext(options);

        var persisted = await verify.Payments
            .AsNoTracking()
            .SingleAsync(x => x.Id == payment.Id, ct);

        var ledger = await verify.LedgerEntries
            .AsNoTracking()
            .Where(x => x.PaymentId == payment.Id)
            .ToListAsync(ct);

        Assert.Equal(PaymentStatus.Refunded, persisted.Status);
        Assert.Equal(2L, persisted.Version);
        Assert.Equal(4, ledger.Count);

        Assert.Equal(2, ledger.Count(x =>
            x.Account == payment.PayerAccount));

        Assert.Equal(2, ledger.Count(x =>
            x.Account == payment.PayeeAccount));
    }
}
