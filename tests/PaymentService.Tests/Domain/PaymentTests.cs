using SwiftRide.PaymentService.Domain.Entities;
using SwiftRide.PaymentService.Domain.Enums;
using Xunit;

namespace SwiftRide.PaymentService.Tests.Domain;

public sealed class PaymentTests
{
    private static Payment NewPayment() => Payment.Create(
        Guid.NewGuid(), 100_000m, Guid.NewGuid().ToString("N"), "rider-001", "driver-001");

    [Fact]
    public void Create_Valid_ShouldStartPendingAndUnsynced()
    {
        var trip = Guid.NewGuid();
        var p = Payment.Create(trip, 100_000m, "k", "rider-001", "driver-001");
        Assert.NotEqual(Guid.Empty, p.Id);
        Assert.Equal(trip, p.TripId);
        Assert.Equal(100_000m, p.Amount);
        Assert.Equal("k", p.IdempotencyKey);
        Assert.Equal("rider-001", p.PayerAccount);
        Assert.Equal("driver-001", p.PayeeAccount);
        Assert.Equal(PaymentStatus.Pending, p.Status);
        Assert.False(p.IsTripSynced);
        Assert.Equal(p.CreatedAt, p.UpdatedAt);
    }

    [Fact]
    public void Create_EmptyTrip_ShouldThrow() => Assert.Throws<ArgumentException>(() =>
        Payment.Create(Guid.Empty, 1, "k", "rider-001", "driver-001"));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_BadAmount_ShouldThrow(decimal amount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Payment.Create(Guid.NewGuid(), amount, "k", "rider-001", "driver-001"));

    [Fact]
    public void Create_TooManyDecimalPlaces_ShouldThrow() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Payment.Create(Guid.NewGuid(), 1.234m, "k", "rider-001", "driver-001"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankKey_ShouldThrow(string key) =>
        Assert.Throws<ArgumentException>(() =>
            Payment.Create(Guid.NewGuid(), 1, key, "rider-001", "driver-001"));

    [Fact]
    public void Create_AccountTooLong_ShouldThrow() =>
        Assert.Throws<ArgumentException>(() =>
            Payment.Create(Guid.NewGuid(), 1, "k", new string('x', 129), "driver-001"));

    [Fact]
    public void MarkSucceeded_FromPending_ShouldWork()
    {
        var p = NewPayment();
        p.MarkSucceeded();
        Assert.Equal(PaymentStatus.Succeeded, p.Status);
    }

    [Fact]
    public void MarkFailed_FromPending_ShouldWork()
    {
        var p = NewPayment();
        p.MarkFailed();
        Assert.Equal(PaymentStatus.Failed, p.Status);
    }

    [Fact]
    public void MarkSucceeded_Twice_ShouldThrow()
    {
        var p = NewPayment();
        p.MarkSucceeded();
        Assert.Throws<InvalidOperationException>(p.MarkSucceeded);
    }

    [Fact]
    public void Refund_AfterSuccess_ShouldWork()
    {
        var p = NewPayment();
        p.MarkSucceeded();
        p.Refund();
        Assert.Equal(PaymentStatus.Refunded, p.Status);
    }

    [Fact]
    public void Refund_WhilePending_ShouldThrow() =>
        Assert.Throws<InvalidOperationException>(NewPayment().Refund);

    [Fact]
    public void MarkTripSynced_FromSucceeded_IsIdempotent()
    {
        var p = NewPayment();
        p.MarkSucceeded();
        p.MarkTripSynced();
        p.MarkTripSynced();
        Assert.True(p.IsTripSynced);
    }

    [Fact]
    public void MarkTripSynced_FromPending_ShouldThrow() =>
        Assert.Throws<InvalidOperationException>(NewPayment().MarkTripSynced);
}
