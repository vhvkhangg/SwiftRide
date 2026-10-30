using Microsoft.EntityFrameworkCore;
using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Infrastructure.Persistence;
using Xunit;

namespace SwiftRide.PaymentService.Tests.Infrastructure;

public sealed class PaymentOutboxModelTests
{
    private static PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Database=model_test;" +
                "Username=unused;Password=unused")
            .Options;

        return new PaymentDbContext(options);
    }

    [Fact]
    public void DbContext_ShouldMapThreeMassTransitTables()
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
    public void InboxState_ShouldHaveUniqueMessageConsumerKey()
    {
        using var db = CreateContext();

        var inbox = db.Model.GetEntityTypes()
            .Single(entity => entity.GetTableName() == "InboxState");

        Assert.Contains(inbox.GetKeys(), key =>
            key.Properties.Count == 2 &&
            key.Properties[0].Name == "MessageId" &&
            key.Properties[1].Name == "ConsumerId");
    }

    [Fact]
    public void Payment_ShouldKeepBusinessUniqueIndexes()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(typeof(Payment));
        Assert.NotNull(entity);

        var keyIndex = entity.GetIndexes().Single(index =>
            index.Properties.Count == 1 &&
            index.Properties[0].Name == nameof(Payment.IdempotencyKey));

        Assert.True(keyIndex.IsUnique);
        Assert.Equal(
            "ux_payments_idempotency_key",
            keyIndex.GetDatabaseName());

        var tripIndex = entity.GetIndexes().Single(index =>
            index.Properties.Count == 1 &&
            index.Properties[0].Name == nameof(Payment.TripId));

        Assert.True(tripIndex.IsUnique);
        Assert.Equal(
            "ux_payments_settled_trip",
            tripIndex.GetDatabaseName());

        Assert.Contains("Succeeded", tripIndex.GetFilter() ?? "");
        Assert.Contains("Refunded", tripIndex.GetFilter() ?? "");
    }

    [Fact]
    public void ConsumerOutboxMigration_ShouldBeRegistered()
    {
        using var db = CreateContext();

        var migrations = db.Database.GetMigrations();

        Assert.Contains(
            "20261022005935_AddPaymentConsumerOutbox",
            migrations);
    }

    [Fact]
    public void PaymentFailureMetadata_ShouldBeNullableAndBounded()
    {
        using var db = CreateContext();
        var payment = db.Model.FindEntityType(typeof(Payment));
        Assert.NotNull(payment);

        var failureCode = payment.FindProperty(nameof(Payment.FailureCode));
        var failureReason = payment.FindProperty(nameof(Payment.FailureReason));
        Assert.NotNull(failureCode);
        Assert.NotNull(failureReason);
        Assert.True(failureCode.IsNullable);
        Assert.True(failureReason.IsNullable);
        Assert.Equal("failure_code", failureCode.GetColumnName());
        Assert.Equal("failure_reason", failureReason.GetColumnName());
        Assert.Equal(64, failureCode.GetMaxLength());
        Assert.Equal(256, failureReason.GetMaxLength());
    }
}
